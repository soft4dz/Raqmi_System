using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Reporting;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP coverage of the automatic-reports module: the routes the desktop client actually
/// calls (/reporting/catalog, /reporting/run, /reporting/executions), the shape they answer, and
/// the single barrier that guards them all - reports.read.
///
/// Running a report IS a read (it creates no business data; the journal line is a trace), so the
/// same key opens the three routes. What this class pins is that the barrier is REALLY there:
/// an authenticated user without reports.read is refused on every one of them, including the
/// journal, which says who pulled which figures.
/// </summary>
public sealed class ReportingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string UnitCode = "RPT-HTTP";

    private readonly RaqmiApiFactory _factory;

    public ReportingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reporting_endpoints_require_authentication()
    {
        using var client = _factory.CreateClient();

        var catalog = await client.GetAsync("/api/v1/reporting/catalog");
        var executions = await client.GetAsync("/api/v1/reporting/executions");

        var run = await client.PostAsJsonAsync(
            "/api/v1/reporting/run",
            new RunReportRequest(ReportCatalog.AgedBalance, new Dictionary<string, string?>
            {
                [ReportCatalog.AsOfDateParameter] = "2026-03-31"
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, catalog.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, executions.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, run.StatusCode);
    }

    /// <summary>
    /// An authenticated profile without reports.read sees nothing of the module - not even the
    /// catalog (which tells what figures can be pulled) nor the journal (which tells what was
    /// pulled, by whom).
    /// </summary>
    [Fact]
    public async Task Every_reporting_route_is_closed_without_reports_read()
    {
        await CreateReportingUserAsync("reporting.norights", "reporting.norights@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("reporting.norights", Password);

        var catalog = await client.GetAsync("/api/v1/reporting/catalog");
        var executions = await client.GetAsync("/api/v1/reporting/executions");

        var run = await client.PostAsJsonAsync(
            "/api/v1/reporting/run",
            new RunReportRequest(ReportCatalog.AgedBalance, new Dictionary<string, string?>
            {
                [ReportCatalog.AsOfDateParameter] = "2026-03-31"
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, catalog.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, executions.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, run.StatusCode);
    }

    /// <summary>
    /// The nominal path of the desktop screen: read the catalog, run a report with its
    /// parameters, then find that execution in the journal - all through HTTP, with the JSON
    /// shapes the client binds to.
    /// </summary>
    [Fact]
    public async Task A_holder_of_reports_read_lists_the_catalog_runs_a_report_and_finds_it_in_the_journal()
    {
        await CreateReportingUserAsync(
            "reporting.reader", "reporting.reader@example.com", PermissionCatalog.ReportsRead);

        await _factory.CreateHotelUnitAsync(UnitCode, "Unité rapports HTTP");

        using var client = await _factory.CreateAuthenticatedClientAsync("reporting.reader", Password);

        var catalog = await client.GetFromJsonAsync<ReportDefinitionResponse[]>(
            "/api/v1/reporting/catalog",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(catalog);
        Assert.Equal(5, catalog!.Length);

        var definition = Assert.Single(catalog, report => report.Code == ReportCatalog.RevenueByUnit);
        Assert.False(string.IsNullOrWhiteSpace(definition.Title));

        // Every parameter is typed for the client's editor: a date picker or a unit picker.
        Assert.All(
            definition.Parameters,
            parameter => Assert.Contains(
                parameter.Type,
                new[] { ReportParameterResponse.Date, ReportParameterResponse.Unit }));

        var runResponse = await client.PostAsJsonAsync(
            "/api/v1/reporting/run",
            new RunReportRequest(ReportCatalog.RevenueByUnit, new Dictionary<string, string?>
            {
                [ReportCatalog.FromParameter] = "2026-03-01",
                [ReportCatalog.ToParameter] = "2026-03-31",
                [ReportCatalog.UnitCodeParameter] = UnitCode
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var result = await runResponse.Content.ReadFromJsonAsync<ReportResultResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(ReportCatalog.RevenueByUnit, result!.ReportCode);
        Assert.Equal(definition.Title, result.Title);
        Assert.NotEmpty(result.Columns);
        Assert.Equal(result.Rows.Count, result.RowCount);

        // The run was journalized, and the journal names its author - the authenticated user,
        // not the service.
        var journal = await client.GetFromJsonAsync<ReportExecutionResponse[]>(
            $"/api/v1/reporting/executions?reportCode={ReportCatalog.RevenueByUnit}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(journal);
        var entry = Assert.Single(journal!);
        Assert.Equal(ReportCatalog.RevenueByUnit, entry.ReportCode);
        Assert.Equal(definition.Title, entry.ReportTitle);
        Assert.Equal("reporting.reader", entry.ExecutedBy);
        Assert.Contains(UnitCode, entry.ParametersJson);

        // Filtering the journal by report code isolates one report: the run above is not in
        // another report's journal.
        var otherJournal = await client.GetFromJsonAsync<ReportExecutionResponse[]>(
            $"/api/v1/reporting/executions?reportCode={ReportCatalog.OccupancyByUnit}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(otherJournal);
        Assert.Empty(otherJournal!);
    }

    /// <summary>
    /// A refused execution is refused with a 400 carrying the reason - never a 500, and never a
    /// journal line: the journal must only ever record figures that were really produced.
    /// </summary>
    [Fact]
    public async Task An_invalid_execution_is_refused_with_a_bad_request_and_leaves_no_journal_line()
    {
        await CreateReportingUserAsync(
            "reporting.badparams", "reporting.badparams@example.com", PermissionCatalog.ReportsRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("reporting.badparams", Password);

        // A required parameter is missing.
        var missing = await client.PostAsJsonAsync(
            "/api/v1/reporting/run",
            new RunReportRequest(ReportCatalog.AgedBalance, new Dictionary<string, string?>()),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // A report that is not in the catalog is a 404, not a 400: the resource does not exist.
        var unknown = await client.PostAsJsonAsync(
            "/api/v1/reporting/run",
            new RunReportRequest("pas-un-rapport", null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var journal = await client.GetFromJsonAsync<ReportExecutionResponse[]>(
            $"/api/v1/reporting/executions?reportCode={ReportCatalog.AgedBalance}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(journal);
        Assert.Empty(journal!);
    }

    /// <summary>
    /// Creates a user carrying a single-purpose role holding exactly the given permission keys
    /// (same technique as the other endpoint test classes), so what is exercised is the barrier
    /// itself, not the composition of a real system role.
    /// </summary>
    private async Task CreateReportingUserAsync(
        string userName,
        string email,
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
            "Reporting permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.reporting.{Guid.NewGuid():N}",
            "Reporting test role",
            "Role dedicated to reporting endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
