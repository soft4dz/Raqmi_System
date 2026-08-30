using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the lodging module. Each test provisions its own dedicated
/// role carrying exactly the lodging permission keys it needs (the keys are seeded from
/// PermissionCatalog by SecuritySeeder during factory startup), so the per-permission policies
/// registered in Program.cs are enforced for real.
///
/// The lodging module CONSUMES the tariff module's <see cref="ITariffResolutionService"/>; these
/// tests pin the resolved rate by swapping that one registration for
/// <see cref="StubTariffResolutionService"/> in a derived factory (same shared SQLite database),
/// so they exercise the lodging workflows without depending on tariff data.
/// </summary>
public sealed class LodgingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string LodgingRead = "lodging.read";
    private const string LodgingWrite = "lodging.write";
    private const string LodgingCheckin = "lodging.checkin";

    private const decimal NightlyRate = 15_000.00m;

    private readonly RaqmiApiFactory _factory;

    public LodgingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Front_desk_cycle_runs_from_room_setup_to_settled_check_out()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("LODHTL", "Lodging Hotel");
        await SeedCustomerAsync("LODCLI", "Client Comptoir");

        await CreateLodgingUserAsync(
            "lodging.desk",
            "lodging.desk@example.com",
            "Front Desk",
            LodgingRead, LodgingWrite, LodgingCheckin);

        using var lodgingFactory = CreateLodgingFactory();
        using var client = await LoginAsync(lodgingFactory, "lodging.desk");

        // Property setup: one room type, one room.
        var roomTypeResponse = await client.PostAsJsonAsync(
            "/api/v1/lodging/room-types",
            new CreateRoomTypeRequest(hotelUnitCode, "dbl", "Chambre double", 2),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, roomTypeResponse.StatusCode);

        var roomType = await roomTypeResponse.Content.ReadFromJsonAsync<RoomTypeResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal("DBL", roomType!.Code);

        var roomResponse = await client.PostAsJsonAsync(
            "/api/v1/lodging/rooms",
            new CreateRoomRequest(hotelUnitCode, "101", "DBL"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, roomResponse.StatusCode);

        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(room);

        // Booking: the nightly rate comes back frozen from the (stubbed) tariff resolution.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDay = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toDay = today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // The dates-first flow: before booking, the availability search lists the room, priced
        // night by night, with the stay total the folio will later bill.
        var availabilityResponse = await client.GetAsync(
            $"/api/v1/lodging/availability?hotelUnitCode={hotelUnitCode}&from={fromDay}&to={toDay}&guests=2");
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);

        var availability = await availabilityResponse.Content.ReadFromJsonAsync<AvailabilityResponse>(RaqmiApiFactory.JsonOptions);
        var availableRoom = Assert.Single(availability!.Rooms);
        Assert.Equal("101", availableRoom.RoomNumber);
        Assert.True(availableRoom.HasRate);
        Assert.Equal(2 * NightlyRate, availableRoom.TotalStayAmount);
        Assert.Equal(2, availableRoom.NightlyRates.Count);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/lodging/reservations",
            new CreateReservationRequest(hotelUnitCode, room!.Id, "lodcli", today, today.AddDays(2), 2),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var reservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(ReservationStatus.Booked, reservation!.Status);
        Assert.Equal("LODCLI", reservation.CustomerCode);
        Assert.Equal(NightlyRate, reservation.NightlyRateSnapshot);
        Assert.Equal("STD", reservation.RatePlanCodeSnapshot);
        Assert.Equal(2, reservation.Nights);

        // The anti-double-booking invariant over HTTP: same room, same dates, refused with 409.
        var doubleBooking = await client.PostAsJsonAsync(
            "/api/v1/lodging/reservations",
            new CreateReservationRequest(hotelUnitCode, room.Id, "LODCLI", today, today.AddDays(2), 1),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, doubleBooking.StatusCode);

        // Check-in on the arrival day opens the folio: one Night line per night at the rate.
        var checkInResponse = await client.PostAsync(
            $"/api/v1/lodging/reservations/{reservation.Id}/check-in", content: null);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);

        var checkedIn = await checkInResponse.Content.ReadFromJsonAsync<ReservationResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(ReservationStatus.CheckedIn, checkedIn!.Status);
        Assert.Equal("lodging.desk", checkedIn.CheckedInBy);

        var folioResponse = await client.GetAsync($"/api/v1/lodging/reservations/{reservation.Id}/folio");
        Assert.Equal(HttpStatusCode.OK, folioResponse.StatusCode);

        var folio = await folioResponse.Content.ReadFromJsonAsync<FolioResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(2, folio!.Charges.Count);
        Assert.All(folio.Charges, charge => Assert.Equal(ChargeKind.Night, charge.Kind));
        Assert.All(folio.Charges, charge => Assert.Equal(NightlyRate, charge.Amount));
        Assert.Equal(2 * NightlyRate, folio.Balance);

        // Once booked, the room disappears from the availability search over the same period.
        var afterBooking = await client.GetAsync(
            $"/api/v1/lodging/availability?hotelUnitCode={hotelUnitCode}&from={fromDay}&to={toDay}&guests=2");
        Assert.Equal(HttpStatusCode.OK, afterBooking.StatusCode);

        var afterBookingAvailability = await afterBooking.Content.ReadFromJsonAsync<AvailabilityResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Empty(afterBookingAvailability!.Rooms);

        // The front-desk snapshot sees the guest in house for the night.
        var frontDeskResponse = await client.GetAsync(
            $"/api/v1/lodging/front-desk?hotelUnitCode={hotelUnitCode}&date={fromDay}");
        Assert.Equal(HttpStatusCode.OK, frontDeskResponse.StatusCode);

        var frontDesk = await frontDeskResponse.Content.ReadFromJsonAsync<FrontDeskResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(1, frontDesk!.InHouseCount);
        Assert.Empty(frontDesk.Arrivals);
        Assert.Empty(frontDesk.Departures);

        // While the guest is in-house, the unit's only room is occupied: 100%.
        var day = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var occupancyResponse = await client.GetAsync(
            $"/api/v1/lodging/occupancy?hotelUnitCode={hotelUnitCode}&from={day}&to={day}");
        Assert.Equal(HttpStatusCode.OK, occupancyResponse.StatusCode);

        var occupancy = await occupancyResponse.Content.ReadFromJsonAsync<OccupancyResponse>(RaqmiApiFactory.JsonOptions);
        var occupancyDay = Assert.Single(occupancy!.Days);
        Assert.Equal(1, occupancyDay.TotalActiveRooms);
        Assert.Equal(1, occupancyDay.OccupiedRooms);
        Assert.Equal(100.00m, occupancyDay.OccupancyRatePercent);

        // Check-out is refused while the folio is not settled to zero.
        var refusedCheckOut = await client.PostAsync(
            $"/api/v1/lodging/reservations/{reservation.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, refusedCheckOut.StatusCode);

        // The normal path: treasury receipt first, then a Settlement line referencing it.
        var settlementResponse = await client.PostAsJsonAsync(
            $"/api/v1/lodging/reservations/{reservation.Id}/folio/charges",
            new AddFolioChargeRequest(today, "Reglement especes", -2 * NightlyRate, ChargeKind.Settlement, "REC-0001"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, settlementResponse.StatusCode);

        var settledFolio = await settlementResponse.Content.ReadFromJsonAsync<FolioResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(0m, settledFolio!.Balance);

        var checkOutResponse = await client.PostAsync(
            $"/api/v1/lodging/reservations/{reservation.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);

        var checkedOut = await checkOutResponse.Content.ReadFromJsonAsync<ReservationResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(ReservationStatus.CheckedOut, checkedOut!.Status);
        Assert.Equal("lodging.desk", checkedOut.CheckedOutBy);
    }

    [Fact]
    public async Task Lodging_permissions_split_reads_setup_and_counter_operations()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("LODSEC", "Lodging Security Hotel");

        await CreateLodgingUserAsync(
            "lodging.viewer",
            "lodging.viewer@example.com",
            "Lodging Viewer",
            LodgingRead);

        await CreateLodgingUserAsync(
            "lodging.setup",
            "lodging.setup@example.com",
            "Lodging Setup",
            LodgingRead, LodgingWrite);

        using var lodgingFactory = CreateLodgingFactory();

        // No token at all: 401 before any permission is even considered.
        using (var anonymousClient = lodgingFactory.CreateClient())
        {
            var anonymous = await anonymousClient.GetAsync("/api/v1/lodging/room-types");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using var viewerClient = await LoginAsync(lodgingFactory, "lodging.viewer");

        var allowedRead = await viewerClient.GetAsync("/api/v1/lodging/room-types");
        Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

        // lodging.read does not grant the setup...
        var forbiddenCreate = await viewerClient.PostAsJsonAsync(
            "/api/v1/lodging/room-types",
            new CreateRoomTypeRequest(hotelUnitCode, "STE", "Suite", 4),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        // ...nor the reservation lifecycle.
        var forbiddenReservation = await viewerClient.PostAsJsonAsync(
            "/api/v1/lodging/reservations",
            new CreateReservationRequest(hotelUnitCode, Guid.NewGuid(), "ANY",
                new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 2), 1),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReservation.StatusCode);

        using var setupClient = await LoginAsync(lodgingFactory, "lodging.setup");

        var allowedCreate = await setupClient.PostAsJsonAsync(
            "/api/v1/lodging/room-types",
            new CreateRoomTypeRequest(hotelUnitCode, "STE", "Suite", 4),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, allowedCreate.StatusCode);

        // lodging.write does not grant the counter operations: check-in is lodging.checkin.
        var forbiddenCheckIn = await setupClient.PostAsync(
            $"/api/v1/lodging/reservations/{Guid.NewGuid()}/check-in", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCheckIn.StatusCode);
    }

    /// <summary>
    /// Derived factory sharing the fixture's SQLite database, with the tariff resolution swapped
    /// for the deterministic stub (the lodging module only consumes that contract).
    /// </summary>
    private WebApplicationFactory<Program> CreateLodgingFactory()
    {
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITariffResolutionService>();
            services.AddSingleton<ITariffResolutionService>(new StubTariffResolutionService(NightlyRate, "STD"));
        }));
    }

    /// <summary>
    /// Logs in through the real HTTP endpoint of the DERIVED factory and returns a client with
    /// the Bearer token set (mirrors RaqmiApiFactory.CreateAuthenticatedClientAsync, which is
    /// not reachable from a WithWebHostBuilder-derived factory).
    /// </summary>
    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string userName)
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userName, Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    /// <summary>
    /// Creates a customer directly through the DbContext, so the lodging tests neither depend on
    /// nor incidentally test the billing customer endpoints.
    /// </summary>
    private async Task SeedCustomerAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        dbContext.Set<Customer>().Add(new Customer(code, name, CustomerType.Individual));
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given lodging
    /// permission keys. The permissions themselves must already exist (SecuritySeeder seeds every
    /// PermissionCatalog entry during factory initialization) - the assertion below fails fast
    /// with a clear signal if the lodging keys have not been added to PermissionCatalog yet.
    /// </summary>
    private async Task CreateLodgingUserAsync(
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
            "Lodging permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.lodging.{Guid.NewGuid():N}",
            "Lodging test role",
            "Role dedicated to lodging endpoint tests.");

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
