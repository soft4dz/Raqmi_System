using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les endpoints /api/v1/kpis en HTTP complet : autorisation, filtrage par permissions,
/// parametrage et cycle de vie des instantanes.
///
/// Le socle de donnees est volontairement reduit aux recettes journalieres et aux tables du
/// module KPI : les formules elles-memes sont prouvees par les tests unitaires des
/// calculateurs, et ces tests-ci verifient LA CHAINE - routes, jetons, politiques, filtrage
/// serveur et persistance - pas l'arithmetique une seconde fois.
/// </summary>
public sealed class KpiEndpointTests(RaqmiApiFactory factory) : IClassFixture<RaqmiApiFactory>, IAsyncLifetime
{
    private const string UnitCode = "KPI-HOTEL";
    private const string DirectionPassword = "Directi0n#2026";
    private const string ReaderPassword = "Lecteur#2026";

    private const string DashboardPath = "/api/v1/kpis/dashboard?from=2026-01-01&to=2026-01-31";

    /// <summary>
    /// xunit instancie la classe - et donc rejoue InitializeAsync - AVANT CHAQUE TEST : le socle
    /// est pose une seule fois et garde par des tests d'existence, sinon la deuxieme execution
    /// violerait les index uniques (code d'unite, nom d'utilisateur) et ferait echouer toute la
    /// classe. Les tests d'une meme classe s'executent sequentiellement : le test-puis-insertion
    /// ne court aucune course.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (!await dbContext.HotelUnits.AnyAsync(unit => unit.Code == UnitCode))
        {
            await factory.CreateHotelUnitAsync(UnitCode, "Hotel des Indicateurs");
        }

        if (!await dbContext.Users.AnyAsync(user => user.UserName == "kpi.direction"))
        {
            await factory.CreateUserAsync(
                "kpi.direction", "kpi.direction@raqmi.test", "Direction KPI", DirectionPassword,
                RoleCatalog.Direction);

            await factory.CreateUserAsync(
                "kpi.reader", "kpi.reader@raqmi.test", "Lecteur KPI", ReaderPassword,
                RoleCatalog.Reader);
        }

        // Une recette VALIDEE de 100 000 (60/25/10/5) et un brouillon enorme qui ne doit
        // jamais apparaitre dans un indicateur.
        if (!await dbContext.DailyRevenues.AnyAsync())
        {
            var validated = new DailyRevenue(
                new DateOnly(2026, 1, 10), UnitCode, 60_000m, 25_000m, 10_000m, 5_000m);

            validated.Submit("tests", DateTimeOffset.UtcNow);
            validated.Validate("tests", DateTimeOffset.UtcNow);

            var draft = new DailyRevenue(
                new DateOnly(2026, 1, 11), UnitCode, 999_999m, 0m, 0m, 0m);

            dbContext.DailyRevenues.AddRange(validated, draft);
            await dbContext.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync(DashboardPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_counts_validated_revenue_only()
    {
        var client = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var dashboard = await client.GetFromJsonAsync<KpiDashboardResponse>(
            DashboardPath, RaqmiApiFactory.JsonOptions);

        Assert.NotNull(dashboard);

        var revenue = dashboard.Sections
            .SelectMany(section => section.Measures)
            .Single(measure => measure.Code == KpiCodes.RevenueTotal);

        // Le brouillon de 999 999 n'est pas du chiffre d'affaires.
        Assert.Equal(100_000m, revenue.Value);

        var food = dashboard.Sections
            .SelectMany(section => section.Measures)
            .Single(measure => measure.Code == KpiCodes.RevenueFood);

        Assert.Equal(25_000m, food.Value);
    }

    [Fact]
    public async Task A_reader_profile_never_receives_payroll_indicators_even_as_ratios()
    {
        // Le profil lecture seule n'a ni hr.read ni treasury.read : la masse salariale et les
        // encaissements ne quittent pas le serveur, et la reponse dit combien de lignes
        // manquent - un ecran qui perd des lignes sans le dire ferait douter du reste.
        var client = await factory.CreateAuthenticatedClientAsync("kpi.reader", ReaderPassword);

        var dashboard = await client.GetFromJsonAsync<KpiDashboardResponse>(
            DashboardPath, RaqmiApiFactory.JsonOptions);

        Assert.NotNull(dashboard);

        var codes = dashboard.Sections
            .SelectMany(section => section.Measures)
            .Select(measure => measure.Code)
            .ToHashSet();

        Assert.DoesNotContain(KpiCodes.PayrollToRevenueRate, codes);
        Assert.DoesNotContain(KpiCodes.PayrollCost, codes);
        Assert.DoesNotContain(KpiCodes.CashIn, codes);
        Assert.Contains(KpiCodes.RevenueTotal, codes);
        Assert.Contains(KpiCodes.OccupancyRate, codes);
        Assert.True(dashboard.HiddenByPermission > 0);
    }

    [Fact]
    public async Task Reading_a_forbidden_indicator_directly_names_the_missing_permissions()
    {
        var client = await factory.CreateAuthenticatedClientAsync("kpi.reader", ReaderPassword);

        var response = await client.GetAsync(
            $"/api/v1/kpis/{KpiCodes.PayrollCost}?from=2026-01-01&to=2026-01-31");

        // Refus EXPLICITE, jamais un NotFound : cacher l'existence d'un indicateur du catalogue
        // public n'apporte rien, dire quelle cle manque permet d'ajuster le profil.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(PermissionCatalog.HrRead, body);
    }

    [Fact]
    public async Task The_catalog_is_published_in_full_with_readability_flags()
    {
        var client = await factory.CreateAuthenticatedClientAsync("kpi.reader", ReaderPassword);

        var catalog = await client.GetFromJsonAsync<KpiCatalogResponse>(
            "/api/v1/kpis", RaqmiApiFactory.JsonOptions);

        Assert.NotNull(catalog);
        Assert.Equal(KpiCatalog.All.Count, catalog.TotalCount);
        Assert.Equal(catalog.ImplementedCount + catalog.AwaitingSourceCount, catalog.TotalCount);

        // Le lecteur voit la bibliotheque ENTIERE - connaitre les indicateurs n'est pas
        // connaitre les chiffres - mais ses fiches disent lesquelles il peut esperer lire.
        Assert.True(catalog.ReadableCount < catalog.TotalCount);
    }

    [Fact]
    public async Task An_oversized_window_is_refused()
    {
        var client = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var response = await client.GetAsync("/api/v1/kpis/dashboard?from=2024-01-01&to=2026-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_comparison_puts_the_group_row_first()
    {
        var client = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var comparison = await client.GetFromJsonAsync<KpiComparisonResponse>(
            "/api/v1/kpis/compare?from=2026-01-01&to=2026-01-31", RaqmiApiFactory.JsonOptions);

        Assert.NotNull(comparison);
        Assert.Null(comparison.Rows.First().HotelUnitCode);
        Assert.Contains(comparison.Rows, row => row.HotelUnitCode == UnitCode);
    }

    [Fact]
    public async Task Threshold_administration_is_gated_by_kpi_admin()
    {
        var request = new SaveKpiThresholdRequest(
            KpiCodes.OccupancyRate, null, 65m, 40m, 70m, "Direction exploitation", null);

        // Le lecteur n'a pas kpi.admin : 403 avant meme d'atteindre le service.
        var reader = await factory.CreateAuthenticatedClientAsync("kpi.reader", ReaderPassword);
        var forbidden = await reader.PutAsJsonAsync("/api/v1/kpis/thresholds", request, RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // La direction le detient : la regle est posee, puis relue.
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);
        var saved = await direction.PutAsJsonAsync("/api/v1/kpis/thresholds", request, RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var thresholds = await direction.GetFromJsonAsync<IReadOnlyCollection<KpiThresholdResponse>>(
            "/api/v1/kpis/thresholds", RaqmiApiFactory.JsonOptions);

        Assert.NotNull(thresholds);
        Assert.Contains(thresholds, threshold => threshold.KpiCode == KpiCodes.OccupancyRate
            && threshold.FavorableThreshold == 65m);
    }

    [Fact]
    public async Task Incoherent_threshold_bounds_are_refused_with_the_domain_message()
    {
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        // Occupation : la hausse est bonne, la borne favorable ne peut pas etre SOUS la critique.
        var response = await direction.PutAsJsonAsync(
            "/api/v1/kpis/thresholds",
            new SaveKpiThresholdRequest(KpiCodes.OccupancyRate, null, 40m, 65m, null, null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_account_mapping_unlocks_nothing_by_itself_but_persists()
    {
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var saved = await direction.PutAsJsonAsync(
            "/api/v1/kpis/account-mappings",
            new SaveKpiAccountMappingRequest("70", KpiAccountGroup.Revenue, "Ventes"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var mappings = await direction.GetFromJsonAsync<IReadOnlyCollection<KpiAccountMappingResponse>>(
            "/api/v1/kpis/account-mappings", RaqmiApiFactory.JsonOptions);

        Assert.NotNull(mappings);
        Assert.Contains(mappings, mapping => mapping.AccountPrefix == "70");
    }

    [Fact]
    public async Task A_letter_bearing_account_prefix_is_refused()
    {
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var response = await direction.PutAsJsonAsync(
            "/api/v1/kpis/account-mappings",
            new SaveKpiAccountMappingRequest("7A", KpiAccountGroup.Revenue, "Invalide"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshots_are_captured_closed_and_then_never_rewritten()
    {
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        // La periode de fevrier est dediee a ce test : le cycle capture -> cloture -> recapture
        // ne doit pas croiser les instantanes d'un autre test de la classe.
        var february = new { From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 2, 28) };

        var captured = await direction.PostAsJsonAsync(
            "/api/v1/kpis/snapshots",
            new CaptureKpiSnapshotsRequest(february.From, february.To, [KpiCodes.RevenueTotal]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, captured.StatusCode);

        var captureBody = await captured.Content.ReadFromJsonAsync<KpiSnapshotBatchResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(captureBody);
        Assert.True(captureBody.Created > 0);

        var closed = await direction.PostAsJsonAsync(
            "/api/v1/kpis/snapshots/close",
            new CloseKpiSnapshotsRequest(february.From, february.To, [KpiCodes.RevenueTotal]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var closeBody = await closed.Content.ReadFromJsonAsync<KpiSnapshotBatchResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(closeBody);
        Assert.Equal(captureBody.Created, closeBody.Closed);

        // Recapture apres cloture : rien n'est reecrit, tout est saute.
        var recaptured = await direction.PostAsJsonAsync(
            "/api/v1/kpis/snapshots",
            new CaptureKpiSnapshotsRequest(february.From, february.To, [KpiCodes.RevenueTotal]),
            RaqmiApiFactory.JsonOptions);

        var recaptureBody = await recaptured.Content.ReadFromJsonAsync<KpiSnapshotBatchResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(recaptureBody);
        Assert.Equal(0, recaptureBody.Created);
        Assert.Equal(0, recaptureBody.Refreshed);
        Assert.True(recaptureBody.SkippedBecauseClosed > 0);

        // L'historique rend les points clotures, avec leur trace.
        var history = await direction.GetFromJsonAsync<KpiHistoryResponse>(
            $"/api/v1/kpis/{KpiCodes.RevenueTotal}/history?from=2026-02-01&to=2026-02-28",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(history);
        var point = Assert.Single(history.Points);
        Assert.Equal(KpiSnapshotStatus.Closed, point.Status);
        Assert.Equal("kpi.direction", point.ClosedBy);
    }

    [Fact]
    public async Task An_unknown_indicator_code_is_a_not_found()
    {
        var direction = await factory.CreateAuthenticatedClientAsync("kpi.direction", DirectionPassword);

        var response = await direction.GetAsync(
            "/api/v1/kpis/PAS_UN_INDICATEUR?from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
