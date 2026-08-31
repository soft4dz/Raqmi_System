using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du module 10.6, centree sur la SEULE elevation de privilege que ce module
/// pourrait introduire.
///
/// Facturer un evenement ecrit une facture reelle, a travers le module Facturation. Si la route ne
/// demandait que mice.write, un commercial pourrait creer des factures sans avoir le droit de
/// facturer : mice.write deviendrait un chemin detourne vers le module Facturation. La route exige
/// donc mice.write ET invoices.write, et ce fichier verrouille cette exigence - si quelqu'un
/// "simplifie" un jour la politique, ces tests tombent.
/// </summary>
public sealed class MiceEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string MiceRead = "mice.read";
    private const string MiceWrite = "mice.write";
    private const string InvoicesWrite = "invoices.write";

    private static readonly DateOnly EventDay = new(2031, 4, 20);

    private readonly RaqmiApiFactory _factory;

    public MiceEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Un_profil_en_lecture_seule_consulte_mais_ne_cree_rien()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("MICE1", "Hotel MICE Lecture");
        await CreateMiceUserAsync("mice.lecteur", "mice.lecteur@example.com", "Lecteur", MiceRead);

        var client = await _factory.CreateAuthenticatedClientAsync("mice.lecteur", Password);

        var listing = await client.GetAsync("/api/v1/mice/spaces");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/mice/spaces/{unitCode}/SALLE9",
            new SaveFunctionSpaceRequest("Salle interdite", 50, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task Sans_invoices_write_un_profil_mice_write_ne_peut_pas_facturer()
    {
        // LE CAS CENTRAL DE CE FICHIER. Le commercial mene l'evenement de bout en bout - salle,
        // devis, confirmation - puis bute sur la facturation, qui n'est pas son droit.
        var unitCode = await _factory.CreateHotelUnitAsync("MICE2", "Hotel MICE Vente");
        var customerCode = await CreateCustomerAsync("MICECLI2", "Societe Seminaire");

        await CreateMiceUserAsync("mice.commercial", "mice.commercial@example.com", "Commercial", MiceWrite, MiceRead);

        var client = await _factory.CreateAuthenticatedClientAsync("mice.commercial", Password);

        var eventId = await ArrangeConfirmedEventAsync(client, unitCode, customerCode, "SALLE2", "EVT-MICE-2");

        var invoiced = await client.PostAsync($"/api/v1/mice/events/{eventId}/invoice", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, invoiced.StatusCode);

        // Et surtout : AUCUNE facture n'a ete creee au passage.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        Assert.False(await dbContext.Invoices.AnyAsync(invoice => invoice.HotelUnitCode == unitCode));
    }

    [Fact]
    public async Task Avec_les_deux_droits_la_facture_est_produite_et_l_evenement_est_gele()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("MICE3", "Hotel MICE Complet");
        var customerCode = await CreateCustomerAsync("MICECLI3", "Societe Congres");

        await CreateMiceUserAsync(
            "mice.responsable",
            "mice.responsable@example.com",
            "Responsable",
            MiceWrite, MiceRead, InvoicesWrite);

        var client = await _factory.CreateAuthenticatedClientAsync("mice.responsable", Password);

        var eventId = await ArrangeConfirmedEventAsync(client, unitCode, customerCode, "SALLE3", "EVT-MICE-3");

        var invoiced = await client.PostAsync($"/api/v1/mice/events/{eventId}/invoice", content: null);
        Assert.Equal(HttpStatusCode.OK, invoiced.StatusCode);

        var booking = await invoiced.Content.ReadFromJsonAsync<EventBookingResponse>();
        Assert.NotNull(booking);
        Assert.NotNull(booking!.InvoiceId);

        // Le devis est desormais gele : le modifier contredirait la facture.
        var reprice = await client.PutAsJsonAsync(
            $"/api/v1/mice/events/{eventId}/lines",
            new[] { new EventBookingLineRequest("Remise", 1m, 1m, 19m) });

        Assert.Equal(HttpStatusCode.Conflict, reprice.StatusCode);
    }

    [Fact]
    public async Task Une_salle_vendue_deux_fois_sur_le_meme_creneau_est_refusee_par_l_API()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("MICE4", "Hotel MICE Conflit");
        var customerCode = await CreateCustomerAsync("MICECLI4", "Societe Gala");

        await CreateMiceUserAsync("mice.vendeur", "mice.vendeur@example.com", "Vendeur", MiceWrite, MiceRead);

        var client = await _factory.CreateAuthenticatedClientAsync("mice.vendeur", Password);

        await client.PostAsJsonAsync(
            $"/api/v1/mice/spaces/{unitCode}/SALLE4",
            new SaveFunctionSpaceRequest("Grand salon", 300, null, null));

        var first = await client.PostAsJsonAsync(
            "/api/v1/mice/events",
            EventRequest(unitCode, "EVT-MICE-4A", "SALLE4", customerCode, new TimeOnly(18, 0), 240, 90, 90));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Vu des invites, le second commence apres la fin du premier (22:00). Mais le premier
        // demonte 90 minutes et le second monte 90 minutes : la salle est disputee.
        var second = await client.PostAsJsonAsync(
            "/api/v1/mice/events",
            EventRequest(unitCode, "EVT-MICE-4B", "SALLE4", customerCode, new TimeOnly(22, 30), 120, 90, 0));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Un_appel_non_authentifie_est_refuse()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/mice/spaces")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/mice/events")).StatusCode);
    }

    // ------------------------------------ Outillage ------------------------------------

    private static CreateEventBookingRequest EventRequest(
        string unitCode,
        string reference,
        string spaceCode,
        string customerCode,
        TimeOnly startTime,
        int durationMinutes,
        int setupMinutes,
        int teardownMinutes)
    {
        return new CreateEventBookingRequest(
            unitCode,
            reference,
            spaceCode,
            customerCode,
            "Congres annuel",
            EventDay,
            startTime,
            durationMinutes,
            setupMinutes,
            teardownMinutes,
            nameof(EventSetupStyle.Banquet),
            120,
            null);
    }

    private static async Task<Guid> ArrangeConfirmedEventAsync(
        HttpClient client,
        string unitCode,
        string customerCode,
        string spaceCode,
        string reference)
    {
        var space = await client.PostAsJsonAsync(
            $"/api/v1/mice/spaces/{unitCode}/{spaceCode}",
            new SaveFunctionSpaceRequest("Grand salon", 300, null, null));

        Assert.Equal(HttpStatusCode.OK, space.StatusCode);

        var created = await client.PostAsJsonAsync(
            "/api/v1/mice/events",
            EventRequest(unitCode, reference, spaceCode, customerCode, new TimeOnly(9, 0), 300, 60, 60));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var booking = await created.Content.ReadFromJsonAsync<EventBookingResponse>();
        Assert.NotNull(booking);

        var priced = await client.PutAsJsonAsync(
            $"/api/v1/mice/events/{booking!.Id}/lines",
            new[]
            {
                new EventBookingLineRequest("Location de salle", 1m, 80_000m, 19m),
                new EventBookingLineRequest("Dejeuner", 120m, 2_500m, 9m)
            });

        Assert.Equal(HttpStatusCode.OK, priced.StatusCode);

        var confirmed = await client.PostAsync($"/api/v1/mice/events/{booking.Id}/confirm", content: null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        return booking.Id;
    }

    private async Task<string> CreateCustomerAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (!await dbContext.Customers.AnyAsync(customer => customer.Code == code))
        {
            dbContext.Customers.Add(new Customer(code, name, CustomerType.Company));
            await dbContext.SaveChangesAsync();
        }

        return code;
    }

    private async Task CreateMiceUserAsync(
        string userName,
        string email,
        string displayName,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Cles de permission absentes du PermissionCatalog seme : " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.mice.{Guid.NewGuid():N}",
            "MICE test role",
            "Role dedie aux tests d'endpoints du module 10.6.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
