using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du référentiel des établissements, centrée sur ce que la généralisation a
/// ajouté : le secteur d'activité et les types non hôteliers traversent l'API (création,
/// lecture, modification), et un client qui ignore encore le champ obtient exactement ce qu'il
/// obtenait avant - un établissement hôtelier, jamais remis en cause par ses modifications.
/// </summary>
public sealed class HotelUnitEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public HotelUnitEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_unit_created_without_a_sector_is_hospitality()
    {
        using var client = await CreateWriterClientAsync("units.legacy");

        // Le corps d'un client antérieur à la notion : ni secteur, ni type nouveau.
        var created = await client.PostAsJsonAsync(
            "/api/v1/organization/hotel-units",
            new { code = "HTL-LEG", name = "Hotel Historique", unitType = "Hotel", displayOrder = 1 },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var unit = await created.Content.ReadFromJsonAsync<HotelUnitResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(unit);
        Assert.Equal(BusinessSector.Hospitality, unit.Sector);
        Assert.Equal(HotelUnitType.Hotel, unit.UnitType);
    }

    [Fact]
    public async Task A_shop_is_created_read_and_updated_with_its_sector()
    {
        using var client = await CreateWriterClientAsync("units.retail");

        var created = await client.PostAsJsonAsync(
            "/api/v1/organization/hotel-units",
            new CreateHotelUnitRequest("BTQ-01", "Boutique Centre", HotelUnitType.Shop, 5, BusinessSector.Retail),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var read = await client.GetFromJsonAsync<HotelUnitResponse>(
            "/api/v1/organization/hotel-units/BTQ-01",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(read);
        Assert.Equal(HotelUnitType.Shop, read.UnitType);
        Assert.Equal(BusinessSector.Retail, read.Sector);

        // Modification SANS secteur : le libellé change, le secteur reste.
        var renamed = await client.PutAsJsonAsync(
            "/api/v1/organization/hotel-units/BTQ-01",
            new { name = "Boutique Centre-Ville", unitType = "Shop", displayOrder = 5 },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var afterRename = await renamed.Content.ReadFromJsonAsync<HotelUnitResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal("Boutique Centre-Ville", afterRename!.Name);
        Assert.Equal(BusinessSector.Retail, afterRename.Sector);

        // Modification AVEC secteur : il suit.
        var reclassified = await client.PutAsJsonAsync(
            "/api/v1/organization/hotel-units/BTQ-01",
            new UpdateHotelUnitRequest("Entrepot Centre", HotelUnitType.Warehouse, 5, BusinessSector.Manufacturing),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, reclassified.StatusCode);

        var afterReclassification = await reclassified.Content.ReadFromJsonAsync<HotelUnitResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(HotelUnitType.Warehouse, afterReclassification!.UnitType);
        Assert.Equal(BusinessSector.Manufacturing, afterReclassification.Sector);

        var listed = await client.GetFromJsonAsync<IReadOnlyCollection<HotelUnitResponse>>(
            "/api/v1/organization/hotel-units",
            RaqmiApiFactory.JsonOptions);

        Assert.Contains(listed!, candidate => candidate.Code == "BTQ-01" && candidate.Sector == BusinessSector.Manufacturing);
    }

    [Fact]
    public async Task An_unknown_sector_is_a_bad_request_not_a_database_error()
    {
        using var client = await CreateWriterClientAsync("units.badsector");

        var response = await client.PostAsJsonAsync(
            "/api/v1/organization/hotel-units",
            new { code = "BAD-01", name = "Secteur inconnu", unitType = "Hotel", displayOrder = 0, sector = "Astrology" },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateWriterClientAsync(string userName)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var permissions = await dbContext.Permissions
                .Where(permission => permission.Key == PermissionCatalog.UnitsRead || permission.Key == PermissionCatalog.UnitsWrite)
                .ToArrayAsync();

            Assert.Equal(2, permissions.Length);

            var role = new Role(
                $"test.units.{Guid.NewGuid():N}",
                "Units test role",
                "Role dedicated to hotel-unit endpoint tests.");

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

        return await _factory.CreateAuthenticatedClientAsync(userName, Password);
    }
}
