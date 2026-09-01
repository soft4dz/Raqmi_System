using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Un hotel de test complet, monte sur une base SQLite ":memory:" isolee par test : une unite, des
/// types de chambres classes en gamme, un parc, un client, et le service PMS branche sur le
/// resolveur tarifaire de test.
///
/// POURQUOI UN HARNESS DEDIE PLUTOT QUE CELUI DE LodgingServiceTests. Les scenarios du PMS -
/// blocages, restrictions, surreservation, changement de type - ont besoin de PLUSIEURS types de
/// chambres classes les uns par rapport aux autres et de plusieurs chambres par type. Les monter a
/// la main dans chaque test noierait ce que chaque test cherche a montrer.
/// </summary>
internal sealed class PmsHarness : IAsyncDisposable
{
    public const string UnitCode = "PMS1";
    public const string CustomerCode = "CLI-PMS";
    public const string StandardType = "DBL";
    public const string SuiteType = "SUI";
    public const decimal NightlyRate = 10_000.00m;

    private readonly SqliteConnection connection;

    private PmsHarness(SqliteConnection connection, RaqmiDbContext dbContext, StubTariffResolutionService resolver)
    {
        this.connection = connection;
        DbContext = dbContext;
        Resolver = resolver;
        Service = new LodgingService(dbContext, new AuditLogWriter(dbContext), resolver);
    }

    public RaqmiDbContext DbContext { get; }

    public StubTariffResolutionService Resolver { get; }

    public LodgingService Service { get; }

    public static OperationContext Context { get; } = new(null, "pms.tester", "127.0.0.1");

    /// <summary>Chambres doubles, dans l'ordre de creation : 101, 102, 103.</summary>
    public IReadOnlyList<Room> StandardRooms { get; private set; } = [];

    /// <summary>Suites : 201.</summary>
    public IReadOnlyList<Room> Suites { get; private set; } = [];

    public static async Task<PmsHarness> CreateAsync(int standardRooms = 3, int suites = 1)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel PMS", HotelUnitType.Hotel));

        // Les deux types sont CLASSES : c'est le rang, et lui seul, qui dit qu'une suite est une
        // montee en gamme par rapport a une double. Sans lui, le systeme ne saurait pas distinguer
        // un surclassement d'un declassement.
        var standard = new RoomType(UnitCode, StandardType, "Double standard", 2);
        standard.SetCommercialProfile(NightlyRate, 22m, rank: 1, displayOrder: 1);

        var suite = new RoomType(UnitCode, SuiteType, "Suite", 4);
        suite.SetCommercialProfile(NightlyRate * 2, 55m, rank: 5, displayOrder: 2);

        dbContext.Set<RoomType>().AddRange(standard, suite);

        var standardList = new List<Room>();

        for (var index = 0; index < standardRooms; index++)
        {
            var room = new Room(UnitCode, $"10{index + 1}", StandardType, floor: "1");
            standardList.Add(room);
            dbContext.Set<Room>().Add(room);
        }

        var suiteList = new List<Room>();

        for (var index = 0; index < suites; index++)
        {
            var room = new Room(UnitCode, $"20{index + 1}", SuiteType, floor: "2");
            suiteList.Add(room);
            dbContext.Set<Room>().Add(room);
        }

        dbContext.Set<Customer>().Add(new Customer(CustomerCode, "Client PMS", CustomerType.Individual));

        await dbContext.SaveChangesAsync();

        var harness = new PmsHarness(connection, dbContext, new StubTariffResolutionService(NightlyRate, "STD"))
        {
            StandardRooms = standardList,
            Suites = suiteList
        };

        return harness;
    }

    /// <summary>Vend une double, avec ou sans chambre affectee.</summary>
    public Task<ApplicationResult<ReservationResponse>> BookAsync(
        DateOnly arrival,
        DateOnly departure,
        Guid? roomId = null,
        string roomTypeCode = StandardType,
        int adults = 2,
        bool allowOverbooking = false,
        bool overrideRestrictions = false,
        Guid? allotmentId = null)
    {
        return Service.CreateReservationAsync(
            new CreateReservationRequest(
                UnitCode,
                roomId,
                CustomerCode,
                arrival,
                departure,
                adults,
                allotmentId,
                GuestName: null,
                RoomTypeCode: roomTypeCode,
                Adults: adults,
                AllowOverbooking: allowOverbooking,
                OverrideRestrictions: overrideRestrictions),
            Context,
            CancellationToken.None);
    }

    /// <summary>Declare la politique d'exploitation de l'unite.</summary>
    public Task<ApplicationResult<LodgingPolicyResponse>> SavePolicyAsync(SaveLodgingPolicyRequest request)
    {
        return Service.SavePolicyAsync(UnitCode, request, Context, CancellationToken.None);
    }

    /// <summary>La politique par defaut, avec les seuls champs que le test veut changer.</summary>
    public static SaveLodgingPolicyRequest DefaultPolicy(
        bool outOfServiceReducesInventory = false,
        bool overbookingEnabled = false,
        bool earlyCheckInIsFree = true,
        decimal earlyCheckInFlatCharge = 0m,
        bool lateCheckOutIsFree = true,
        decimal lateCheckOutFlatCharge = 0m,
        TimeOnly? lateCheckOutUntilTime = null)
    {
        return new SaveLodgingPolicyRequest(
            new TimeOnly(14, 0),
            new TimeOnly(12, 0),
            EarlyCheckInFromTime: null,
            EarlyCheckInIsFree: earlyCheckInIsFree,
            EarlyCheckInFlatCharge: earlyCheckInFlatCharge,
            EarlyCheckInPercentOfNight: 0m,
            LateCheckOutUntilTime: lateCheckOutUntilTime,
            LateCheckOutIsFree: lateCheckOutIsFree,
            LateCheckOutFlatCharge: lateCheckOutFlatCharge,
            LateCheckOutPercentOfNight: 0m,
            OutOfServiceReducesInventory: outOfServiceReducesInventory,
            OverbookingEnabled: overbookingEnabled);
    }

    /// <summary>La disponibilite d'un type sur une periode, telle que le moteur la calcule.</summary>
    public async Task<RoomTypeAvailabilityResponse> AvailabilityForAsync(
        DateOnly from,
        DateOnly to,
        string roomTypeCode = StandardType,
        int adults = 2,
        bool allowOverbooking = false)
    {
        var result = await Service.SearchAvailabilityAsync(
            new AvailabilitySearchRequest(
                UnitCode,
                from,
                to,
                Adults: adults,
                RoomTypeCode: roomTypeCode,
                AllowOverbooking: allowOverbooking),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value!.RoomTypes);

        return Assert.Single(result.Value.RoomTypes!);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await connection.DisposeAsync();
    }
}
