using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration harness built on <see cref="WebApplicationFactory{TEntryPoint}"/> against
/// the real <c>Program</c> composition root (routing, JSON binding, JwtBearer authentication, and
/// the per-permission authorization policies registered in Program.cs all run for real here -
/// nothing is swapped out except the database).
///
/// The factory forces the "Development" hosting environment so that
/// <c>JwtOptions.FromConfiguration(..., allowEphemeralDevelopmentKey: true)</c> mints a random
/// signing key for the process, and it replaces the production Npgsql-backed
/// <c>DbContextOptions&lt;RaqmiDbContext&gt;</c> registration with a SQLite ":memory:" database.
///
/// A SQLite ":memory:" database only lives as long as its connection stays open (closing the
/// connection destroys the schema and data), so the connection is opened explicitly here and kept
/// open for the whole lifetime of the factory - the same technique already used by
/// RefreshTokenRotationTests.cs. This also means ExecuteUpdateAsync (used by
/// AuthenticationService.RefreshAsync for atomic refresh-token rotation) works, unlike with the EF
/// Core InMemory provider.
///
/// One factory instance is meant to back exactly one test class (via IClassFixture&lt;RaqmiApiFactory&gt;)
/// so that each test class gets its own isolated, dedicated in-memory database - test classes never
/// share state, only tests within the same class share the seeded permissions/roles.
/// </summary>
public sealed class RaqmiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The API serializes responses (and binds request bodies) using camelCase, case-insensitive
    /// JSON with enums written as strings (see Program.cs's ConfigureHttpJsonOptions). Tests must
    /// mirror that exact configuration on the client side, or round-tripping the API's own
    /// PascalCase C# record types (LoginResponse, DailyRevenueResponse, ...) through
    /// HttpClient's default (case-sensitive, no enum converter) JSON options silently produces
    /// nulls/defaults instead of a deserialization error.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // AddDbContext<RaqmiDbContext> (called by AddRaqmiInfrastructure with UseNpgsql) does
            // not only register DbContextOptions<RaqmiDbContext> - starting with EF Core 8, each
            // call also adds an internal IDbContextOptionsConfiguration<RaqmiDbContext> singleton
            // that accumulates rather than replaces, so removing only DbContextOptions<T> leaves
            // the original UseNpgsql configuration action registered too. With both configuration
            // actions applied, the resulting DbContextOptions ends up carrying both the Npgsql and
            // the Sqlite provider extensions at once, which EF Core rejects at runtime
            // ("Only a single database provider can be registered"). Stripping every service
            // descriptor that references RaqmiDbContext in its (possibly generic) service type -
            // not just DbContextOptions<RaqmiDbContext> itself - clears that internal
            // configuration-accumulation list too, regardless of its (non-public) exact type.
            var raqmiDbContextDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(RaqmiDbContext) ||
                    (descriptor.ServiceType.IsGenericType &&
                        descriptor.ServiceType.GetGenericArguments().Contains(typeof(RaqmiDbContext))))
                .ToList();

            foreach (var descriptor in raqmiDbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaqmiDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    /// <summary>
    /// xunit calls this once, right after construction, before any test in the fixture's class
    /// runs. The connection must be opened before the host is first built (triggered here by
    /// touching <see cref="WebApplicationFactory{TEntryPoint}.Services"/>) so the SQLite
    /// ":memory:" database is already alive by the time EF Core issues its first command against it.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        // EnsureCreated (not migrations) builds the schema straight from the current model -
        // sufficient for tests and avoids depending on the Npgsql-targeted migration history.
        await dbContext.Database.EnsureCreatedAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<ISecuritySeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }

    /// <summary>
    /// Creates a test user with a known password, assigned to a single named system role
    /// (see RoleCatalog), reusing the same IPasswordHasher the production sign-in path verifies
    /// against. The role must already exist (SecuritySeeder.SeedAsync, called from
    /// InitializeAsync, seeds every RoleCatalog role before this can be called).
    /// </summary>
    public async Task<Guid> CreateUserAsync(
        string userName,
        string email,
        string displayName,
        string password,
        string roleName)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var role = await dbContext.Roles.SingleAsync(candidate => candidate.Name == roleName);

        var user = new User(userName, email, displayName, passwordHasher.Hash(password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Creates a hotel unit directly through the DbContext (bypassing the organization API
    /// endpoints entirely), so revenue-workflow tests can set up the unit they depend on without
    /// coupling to - or incidentally testing - the hotel-units module.
    /// </summary>
    public async Task<string> CreateHotelUnitAsync(
        string code,
        string name,
        HotelUnitType unitType = HotelUnitType.Hotel)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var hotelUnit = new HotelUnit(code, name, unitType);
        dbContext.HotelUnits.Add(hotelUnit);
        await dbContext.SaveChangesAsync();

        return hotelUnit.Code;
    }

    /// <summary>
    /// Writes the global settings singleton directly through the DbContext (bypassing the settings
    /// API), so tests that need to ISSUE an invoice satisfy the emitter-identity guard without
    /// having to grant themselves settings.write or incidentally test the settings module.
    /// Idempotent: the singleton is written at most once per factory, whatever the order xunit
    /// runs the class's tests in. Defaults describe a fully identified establishment; pass null
    /// for a mention to reproduce an incompletely configured installation.
    /// </summary>
    public async Task ConfigureApplicationSettingsAsync(
        string companyName = "Hotel El Manar Spa",
        string? companyNif = "098765432112345",
        string? companyRc = "16/00-1234567B99",
        string? companyAi = "16012345678",
        string? companyNis = "543211234509876",
        string? companyAddress = "Boulevard des Martyrs",
        string? companyCity = "Alger")
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var existing = await dbContext.Set<ApplicationSettings>()
            .SingleOrDefaultAsync(current => current.SingletonKey == ApplicationSettings.SingletonKeyValue);

        if (existing is not null)
        {
            return;
        }

        var settings = new ApplicationSettings(
            companyName,
            companyNif,
            companyRc,
            companyAi,
            companyNis,
            companyAddress,
            companyCity);

        settings.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<ApplicationSettings>().Add(settings);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Logs in through the real HTTP endpoint (POST /api/v1/auth/login) and returns an
    /// HttpClient with the resulting access token already set as a Bearer Authorization header,
    /// so callers can immediately exercise authorization-policy-protected endpoints.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userNameOrEmail, string password)
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userNameOrEmail, password),
            JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
