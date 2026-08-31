using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the housekeeping module (module 10.2). Each test provisions
/// its own dedicated role carrying exactly the permission keys it needs, so the per-permission
/// policies registered in Program.cs are enforced for real.
///
/// The module READS the lodging module (reservations drive the day sheet) and WRITES through it
/// (a minibar consumption becomes a folio line), so these tests set up real rooms and stays over
/// HTTP, with the tariff resolution swapped for the deterministic stub - the same technique as
/// <see cref="LodgingEndpointTests"/>.
/// </summary>
public sealed class HousekeepingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string HousekeepingRead = "housekeeping.read";
    private const string HousekeepingWrite = "housekeeping.write";
    private const string HousekeepingInspect = "housekeeping.inspect";

    private const string LodgingRead = "lodging.read";
    private const string LodgingWrite = "lodging.write";
    private const string LodgingCheckin = "lodging.checkin";

    private const decimal NightlyRate = 15_000.00m;

    private readonly RaqmiApiFactory _factory;

    public HousekeepingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Day_sheet_is_generated_from_reservations_and_regenerating_preserves_the_morning_work()
    {
        var unit = await _factory.CreateHotelUnitAsync("HKGEN", "Housekeeping Generation");
        await SeedCustomerAsync("HKCLI1", "Client Housekeeping");

        await CreateHousekeepingUserAsync(
            "hk.generate",
            "hk.generate@example.com",
            "Gouvernante",
            HousekeepingRead, HousekeepingWrite, LodgingRead, LodgingWrite, LodgingCheckin);

        using var factory = CreateHousekeepingFactory();
        using var client = await LoginAsync(factory, "hk.generate");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await CreateRoomTypeAsync(client, unit, "DBL", "Chambre double", 2);

        // 101 hosts a stay that ENDS today  -> departure clean.
        // 102 hosts a stay that RUNS today  -> stayover service.
        // 103 is free and nobody declared it dirty -> no work at all.
        var departureRoom = await CreateRoomAsync(client, unit, "101", "DBL");
        var stayoverRoom = await CreateRoomAsync(client, unit, "102", "DBL");
        await CreateRoomAsync(client, unit, "103", "DBL");

        var departure = await CreateReservationAsync(client, unit, departureRoom.Id, "HKCLI1", today.AddDays(-2), today);
        var stayover = await CreateReservationAsync(client, unit, stayoverRoom.Id, "HKCLI1", today.AddDays(-1), today.AddDays(2));

        await CheckInAsync(client, departure.Id);
        await CheckInAsync(client, stayover.Id);

        var generated = await GenerateAsync(client, unit, today);

        Assert.Equal(2, generated.Created);
        Assert.Equal(0, generated.SkippedExisting);

        Assert.Equal(
            HousekeepingTaskType.Departure,
            Assert.Single(generated.Tasks, task => task.RoomNumber == "101").TaskType);

        Assert.Equal(
            HousekeepingTaskType.Stayover,
            Assert.Single(generated.Tasks, task => task.RoomNumber == "102").TaskType);

        Assert.DoesNotContain(generated.Tasks, task => task.RoomNumber == "103");

        // Idempotence: re-running after a late booking must preserve the morning work, and SAY
        // that it did rather than look like a silent rebuild.
        var again = await GenerateAsync(client, unit, today);

        Assert.Equal(0, again.Created);
        Assert.Equal(2, again.SkippedExisting);
    }

    [Fact]
    public async Task Out_of_order_rooms_are_kept_off_the_sheet()
    {
        var unit = await _factory.CreateHotelUnitAsync("HKOOO", "Housekeeping Out Of Order");
        await SeedCustomerAsync("HKCLI2", "Client Hors Service");

        await CreateHousekeepingUserAsync(
            "hk.outoforder",
            "hk.outoforder@example.com",
            "Gouvernante HS",
            HousekeepingRead, HousekeepingWrite, LodgingRead, LodgingWrite, LodgingCheckin);

        using var factory = CreateHousekeepingFactory();
        using var client = await LoginAsync(factory, "hk.outoforder");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await CreateRoomTypeAsync(client, unit, "DBL", "Chambre double", 2);
        var room = await CreateRoomAsync(client, unit, "201", "DBL");

        var stay = await CreateReservationAsync(client, unit, room.Id, "HKCLI2", today.AddDays(-1), today);
        await CheckInAsync(client, stay.Id);

        // A withdrawal without a reason is refused: it is what makes the withdrawal actionable.
        var missingReason = await client.PostAsJsonAsync(
            $"/api/v1/housekeeping/rooms/{room.Id}/condition",
            new SetRoomConditionRequest(RoomConditionStatus.OutOfOrder),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var withdraw = await client.PostAsJsonAsync(
            $"/api/v1/housekeeping/rooms/{room.Id}/condition",
            new SetRoomConditionRequest(RoomConditionStatus.OutOfOrder, "Fuite salle de bain"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        // Sending an attendant into a room under repair is exactly what the withdrawal prevents.
        var generated = await GenerateAsync(client, unit, today);

        Assert.Equal(0, generated.Created);
        Assert.Equal(1, generated.SkippedOutOfOrder);

        var board = await GetBoardAsync(client, unit, today);
        var boardRow = Assert.Single(board.Rows);

        Assert.Equal(RoomConditionStatus.OutOfOrder, boardRow.ConditionStatus);
        Assert.Equal(RoomOccupancyState.Departure, boardRow.OccupancyState);
        Assert.Equal(1, board.OutOfOrderRooms);
    }

    [Fact]
    public async Task Task_cycle_drives_the_room_condition_and_a_refusal_sends_the_room_back_to_dirty()
    {
        var unit = await _factory.CreateHotelUnitAsync("HKCYC", "Housekeeping Cycle");
        await SeedCustomerAsync("HKCLI3", "Client Cycle");

        await CreateHousekeepingUserAsync(
            "hk.cycle",
            "hk.cycle@example.com",
            "Gouvernante Cycle",
            HousekeepingRead, HousekeepingWrite, HousekeepingInspect, LodgingRead, LodgingWrite, LodgingCheckin);

        using var factory = CreateHousekeepingFactory();
        using var client = await LoginAsync(factory, "hk.cycle");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await CreateRoomTypeAsync(client, unit, "DBL", "Chambre double", 2);
        var room = await CreateRoomAsync(client, unit, "301", "DBL");

        var stay = await CreateReservationAsync(client, unit, room.Id, "HKCLI3", today.AddDays(-1), today);
        await CheckInAsync(client, stay.Id);

        var task = Assert.Single((await GenerateAsync(client, unit, today)).Tasks);

        // Starting before assigning is refused by the domain, surfaced as a conflict: the caller
        // is acting on a state the sheet does not show.
        var earlyStart = await client.PostAsync($"/api/v1/housekeeping/tasks/{task.Id}/start", content: null);
        Assert.Equal(HttpStatusCode.Conflict, earlyStart.StatusCode);

        await PostTaskActionAsync(client, task.Id, "assign", new AssignHousekeepingTaskRequest("Amina"));
        await PostTaskActionAsync(client, task.Id, "start", payload: null);

        var cleaned = await PostTaskActionAsync(
            client,
            task.Id,
            "complete",
            new CompleteHousekeepingTaskRequest("Chambre faite"));

        Assert.Equal(HousekeepingTaskStatus.Cleaned, cleaned.Status);

        // Completing the task made the room sellable again, in the same movement.
        var afterCleaning = Assert.Single((await GetBoardAsync(client, unit, today)).Rows);
        Assert.Equal(RoomConditionStatus.Clean, afterCleaning.ConditionStatus);
        Assert.True(afterCleaning.ConditionRecorded);

        // A refusal needs a reason, and puts the room back where it really is.
        var refusalWithoutReason = await client.PostAsJsonAsync(
            $"/api/v1/housekeeping/tasks/{task.Id}/inspect",
            new InspectHousekeepingTaskRequest(Accepted: false),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusalWithoutReason.StatusCode);

        var rejected = await PostTaskActionAsync(
            client,
            task.Id,
            "inspect",
            new InspectHousekeepingTaskRequest(Accepted: false, "Salle de bain non faite"));

        Assert.Equal(HousekeepingTaskStatus.Rejected, rejected.Status);

        var afterRefusal = Assert.Single((await GetBoardAsync(client, unit, today)).Rows);
        Assert.Equal(RoomConditionStatus.Dirty, afterRefusal.ConditionStatus);

        // Second pass, then acceptance: the room ends inspected and the task is closed.
        await PostTaskActionAsync(client, task.Id, "start", payload: null);
        await PostTaskActionAsync(client, task.Id, "complete", new CompleteHousekeepingTaskRequest());

        var accepted = await PostTaskActionAsync(
            client,
            task.Id,
            "inspect",
            new InspectHousekeepingTaskRequest(Accepted: true));

        Assert.Equal(HousekeepingTaskStatus.Inspected, accepted.Status);

        var afterAcceptance = Assert.Single((await GetBoardAsync(client, unit, today)).Rows);
        Assert.Equal(RoomConditionStatus.Inspected, afterAcceptance.ConditionStatus);

        // The planning view sees the same day through the attendant it was handed to.
        var sheetResponse = await client.GetAsync(
            $"/api/v1/housekeeping/day-sheet?hotelUnitCode={unit}&date={Format(today)}");

        Assert.Equal(HttpStatusCode.OK, sheetResponse.StatusCode);

        var sheet = await sheetResponse.Content.ReadFromJsonAsync<HousekeepingDaySheetResponse>(RaqmiApiFactory.JsonOptions);
        var attendant = Assert.Single(sheet!.Attendants);

        Assert.Equal("Amina", attendant.AssignedTo);
        Assert.Equal(1, attendant.TaskCount);
        Assert.Equal(1, attendant.Inspected);
        Assert.Equal(0, sheet.UnassignedTasks);
    }

    [Fact]
    public async Task Minibar_consumption_is_billed_on_the_folio_at_the_frozen_price()
    {
        var unit = await _factory.CreateHotelUnitAsync("HKMBR", "Housekeeping Minibar");
        await SeedCustomerAsync("HKCLI4", "Client Minibar");

        await CreateHousekeepingUserAsync(
            "hk.minibar",
            "hk.minibar@example.com",
            "Comptoir Minibar",
            HousekeepingRead, HousekeepingWrite, LodgingRead, LodgingWrite, LodgingCheckin);

        using var factory = CreateHousekeepingFactory();
        using var client = await LoginAsync(factory, "hk.minibar");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await CreateRoomTypeAsync(client, unit, "DBL", "Chambre double", 2);
        var room = await CreateRoomAsync(client, unit, "401", "DBL");

        var stay = await CreateReservationAsync(client, unit, room.Id, "HKCLI4", today, today.AddDays(1));

        var itemResponse = await client.PostAsJsonAsync(
            "/api/v1/housekeeping/minibar/items",
            new CreateMinibarItemRequest(unit, "eau50", "Eau minerale 50 cl", 120.00m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);

        var item = await itemResponse.Content.ReadFromJsonAsync<MinibarItemResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal("EAU50", item!.Code);

        // A stay that is only booked has no folio to bill: the refusal comes from the lodging
        // folio path this goes through, not from a rule re-implemented in housekeeping.
        var beforeCheckIn = await client.PostAsJsonAsync(
            "/api/v1/housekeeping/minibar/consumptions",
            new RecordMinibarConsumptionRequest(stay.Id, "EAU50", 2, today),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, beforeCheckIn.StatusCode);

        await CheckInAsync(client, stay.Id);

        var recordResponse = await client.PostAsJsonAsync(
            "/api/v1/housekeeping/minibar/consumptions",
            new RecordMinibarConsumptionRequest(stay.Id, "eau50", 2, today),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, recordResponse.StatusCode);

        var consumption = await recordResponse.Content.ReadFromJsonAsync<MinibarConsumptionResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(240.00m, consumption!.TotalAmount);
        Assert.Equal("401", consumption.RoomNumber);

        // The money landed on the folio, and the line points back at the housekeeping record.
        var folioResponse = await client.GetAsync($"/api/v1/lodging/reservations/{stay.Id}/folio");
        Assert.Equal(HttpStatusCode.OK, folioResponse.StatusCode);

        var folio = await folioResponse.Content.ReadFromJsonAsync<FolioResponse>(RaqmiApiFactory.JsonOptions);
        var extra = Assert.Single(folio!.Charges, charge => charge.Kind == ChargeKind.Extra);

        Assert.Equal(240.00m, extra.Amount);
        Assert.Equal(consumption.Id.ToString(), extra.Reference);
        Assert.Contains("Eau minerale 50 cl", extra.Label, StringComparison.Ordinal);

        // Revising the price list must never rewrite what a guest was already charged.
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/housekeeping/minibar/items/{item.Id}",
            new UpdateMinibarItemRequest("Eau minerale 50 cl", 200.00m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var consumptionsResponse = await client.GetAsync(
            $"/api/v1/housekeeping/minibar/consumptions?hotelUnitCode={unit}");

        var consumptions = await consumptionsResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<MinibarConsumptionResponse>>(RaqmiApiFactory.JsonOptions);

        Assert.Equal(120.00m, Assert.Single(consumptions!).UnitPrice);

        // A product withdrawn from the card can no longer be charged.
        var deactivate = await client.PostAsync(
            $"/api/v1/housekeeping/minibar/items/{item.Id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var afterWithdrawal = await client.PostAsJsonAsync(
            "/api/v1/housekeeping/minibar/consumptions",
            new RecordMinibarConsumptionRequest(stay.Id, "EAU50", 1, today),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, afterWithdrawal.StatusCode);
    }

    [Fact]
    public async Task Inspection_is_a_permission_of_its_own()
    {
        var unit = await _factory.CreateHotelUnitAsync("HKPRM", "Housekeeping Permissions");

        await CreateHousekeepingUserAsync(
            "hk.viewer",
            "hk.viewer@example.com",
            "Lecture Housekeeping",
            HousekeepingRead);

        await CreateHousekeepingUserAsync(
            "hk.attendant",
            "hk.attendant@example.com",
            "Agent Housekeeping",
            HousekeepingRead, HousekeepingWrite);

        using var factory = CreateHousekeepingFactory();

        using var viewer = await LoginAsync(factory, "hk.viewer");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reading is allowed...
        var board = await viewer.GetAsync(
            $"/api/v1/housekeeping/board?hotelUnitCode={unit}&date={Format(today)}");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);

        // ...writing is not.
        var generate = await viewer.PostAsJsonAsync(
            "/api/v1/housekeeping/tasks/generate",
            new GenerateHousekeepingTasksRequest(unit, today),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, generate.StatusCode);

        // housekeeping.write runs the sheet but does NOT sign a room off: the attendant who
        // cleaned the room must not be the one who accepts it.
        using var attendant = await LoginAsync(factory, "hk.attendant");

        var allowedWrite = await attendant.PostAsJsonAsync(
            "/api/v1/housekeeping/tasks/generate",
            new GenerateHousekeepingTasksRequest(unit, today),
            RaqmiApiFactory.JsonOptions);
        Assert.NotEqual(HttpStatusCode.Forbidden, allowedWrite.StatusCode);

        var forbiddenInspection = await attendant.PostAsJsonAsync(
            $"/api/v1/housekeeping/tasks/{Guid.NewGuid()}/inspect",
            new InspectHousekeepingTaskRequest(Accepted: true),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenInspection.StatusCode);
    }

    // ------------------------------------------------------------------------------ helpers

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<GenerateHousekeepingTasksResponse> GenerateAsync(
        HttpClient client,
        string hotelUnitCode,
        DateOnly date)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/housekeeping/tasks/generate",
            new GenerateHousekeepingTasksRequest(hotelUnitCode, date),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GenerateHousekeepingTasksResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task<RoomBoardResponse> GetBoardAsync(
        HttpClient client,
        string hotelUnitCode,
        DateOnly date)
    {
        var response = await client.GetAsync(
            $"/api/v1/housekeeping/board?hotelUnitCode={hotelUnitCode}&date={Format(date)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<RoomBoardResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task<HousekeepingTaskResponse> PostTaskActionAsync(
        HttpClient client,
        Guid taskId,
        string action,
        object? payload)
    {
        var response = payload is null
            ? await client.PostAsync($"/api/v1/housekeeping/tasks/{taskId}/{action}", content: null)
            : await client.PostAsJsonAsync($"/api/v1/housekeeping/tasks/{taskId}/{action}", payload, RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<HousekeepingTaskResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task CreateRoomTypeAsync(
        HttpClient client,
        string hotelUnitCode,
        string code,
        string label,
        int capacity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/lodging/room-types",
            new CreateRoomTypeRequest(hotelUnitCode, code, label, capacity),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<RoomResponse> CreateRoomAsync(
        HttpClient client,
        string hotelUnitCode,
        string number,
        string roomTypeCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/lodging/rooms",
            new CreateRoomRequest(hotelUnitCode, number, roomTypeCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<RoomResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task<ReservationResponse> CreateReservationAsync(
        HttpClient client,
        string hotelUnitCode,
        Guid roomId,
        string customerCode,
        DateOnly arrival,
        DateOnly departure)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/lodging/reservations",
            new CreateReservationRequest(hotelUnitCode, roomId, customerCode, arrival, departure, 1),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ReservationResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task CheckInAsync(HttpClient client, Guid reservationId)
    {
        var response = await client.PostAsync(
            $"/api/v1/lodging/reservations/{reservationId}/check-in", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Derived factory sharing the fixture's SQLite database, with the tariff resolution swapped
    /// for the deterministic stub: these tests need reservations to exist, not a tariff grid.
    /// </summary>
    private WebApplicationFactory<Program> CreateHousekeepingFactory()
    {
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITariffResolutionService>();
            services.AddSingleton<ITariffResolutionService>(new StubTariffResolutionService(NightlyRate, "STD"));
        }));
    }

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

    private async Task SeedCustomerAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        dbContext.Set<Customer>().Add(new Customer(code, name, CustomerType.Individual));
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given
    /// permission keys. The assertion fails fast with a clear signal if a key is missing from
    /// PermissionCatalog (the seeder seeds every catalog entry at factory initialization).
    /// </summary>
    private async Task CreateHousekeepingUserAsync(
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
            "Permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.housekeeping.{Guid.NewGuid():N}",
            "Housekeeping test role",
            "Role dedicated to housekeeping endpoint tests.");

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
