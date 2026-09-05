using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Net.Http.Json;

namespace RaqmiSystem.Tests;

/// <summary>
/// POST /lodging/reservations/{id}/folios/{folioId}/invoice en HTTP : la double cle (lodging.checkout
/// ET invoices.write, comme la facturation d'un evenement MICE), puis la facture creee et
/// rattachee au folio.
/// </summary>
public sealed class LodgingBillingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";
    private const string UnitCode = "FOLHTL";

    private readonly RaqmiApiFactory _factory;

    public LodgingBillingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Facturer_un_folio_exige_le_depart_et_la_facturation_puis_rattache_la_facture()
    {
        await _factory.CreateHotelUnitAsync(UnitCode, "Folio Hotel");

        var (reservationId, folioId) = await SeedStayAsync();

        await CreateUserAsync("folio.desk", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingCheckout);
        await CreateUserAsync("folio.cashier", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingCheckout, PermissionCatalog.InvoicesWrite);

        using var desk = await _factory.CreateAuthenticatedClientAsync("folio.desk", Password);
        using var cashier = await _factory.CreateAuthenticatedClientAsync("folio.cashier", Password);

        var route = $"/api/v1/lodging/reservations/{reservationId}/folios/{folioId}/invoice";

        // lodging.checkout seul : le comptoir ne cree pas de facture.
        var forbidden = await desk.PostAsync(route, content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var invoiced = await cashier.PostAsync(route, content: null);
        Assert.Equal(HttpStatusCode.OK, invoiced.StatusCode);

        var response = await invoiced.Content.ReadFromJsonAsync<FolioInvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(response);
        Assert.Equal(folioId, response!.FolioId);
        Assert.Equal(InvoiceStatus.Draft, response.Invoice.Status);
        Assert.Equal(12_000m, response.Invoice.TotalInclVat);
        Assert.Equal(response.FolioTotalCharges, response.Invoice.TotalInclVat);

        // Deja facture : 409, et le folio porte bien la facture.
        var again = await cashier.PostAsync(route, content: null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var folios = await cashier.GetFromJsonAsync<IReadOnlyCollection<FolioResponse>>(
            $"/api/v1/lodging/reservations/{reservationId}/folios",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(response.Invoice.Id, Assert.Single(folios!).InvoiceId);
    }

    /// <summary>
    /// Un sejour en cours avec son folio client (une nuit a 10 000 sans TVA renseignee, un extra
    /// a 2 000 TTC a 19 %), ecrit directement : la route sous test est la facturation, pas la
    /// vente ni l'arrivee.
    /// </summary>
    private async Task<(Guid ReservationId, Guid FolioId)> SeedStayAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        dbContext.Set<Customer>().Add(new Customer("FOLCLI", "Client Folio", CustomerType.Individual));
        dbContext.Set<RoomType>().Add(new RoomType(UnitCode, "DBL", "Double", 2));

        var room = new Room(UnitCode, "101", "DBL");
        dbContext.Set<Room>().Add(room);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reservation = TestReservations.Create(UnitCode, room.Id, "FOLCLI", today, today.AddDays(1), 2, 10_000m, "STD");
        dbContext.Set<Reservation>().Add(reservation);

        var folio = new Folio(reservation.Id, UnitCode, $"F-{reservation.Number}");
        folio.AddCharge(new FolioCharge(today, $"Nuitee du {today:dd/MM/yyyy}", 10_000m, ChargeKind.Night));
        folio.AddCharge(new FolioCharge(today, "Diner", 2_000m, ChargeKind.Extra, vatRate: 19m));
        dbContext.Set<Folio>().Add(folio);

        await dbContext.SaveChangesAsync();

        return (reservation.Id, folio.Id);
    }

    private async Task CreateUserAsync(string userName, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.Equal(permissionKeys.Length, permissions.Length);

        var role = new Role($"test.folio.{Guid.NewGuid():N}", "Folio invoice test role", "Role dedie aux tests de facturation du folio.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, $"{userName}@example.com", userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
