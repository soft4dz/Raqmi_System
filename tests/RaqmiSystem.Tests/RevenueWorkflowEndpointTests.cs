using RaqmiSystem.Domain.Organization;
using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the daily-revenue create/submit/validate workflow. Unlike
/// DailyRevenueTests.cs (which exercises IDailyRevenueService directly), this class drives the
/// real endpoints so the [Authorize]-equivalent per-permission policies registered in
/// Program.cs (RequireAuthorization(PermissionCatalog.RevenueWrite) etc.) are actually enforced by
/// the ASP.NET Core authorization middleware - a caller without the right permission claim must
/// get a real 403, not just "would have been blocked in theory".
/// </summary>
public sealed class RevenueWorkflowEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public RevenueWorkflowEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Daily_revenue_can_be_created_submitted_and_validated_by_users_with_the_right_permissions()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("WFHTL", "Workflow Hotel");

        await _factory.CreateUserAsync(
            "workflow.writer",
            "workflow.writer@example.com",
            "Workflow Writer",
            Password,
            RoleCatalog.UnitManager); // has revenue.write, not revenue.validate

        await _factory.CreateUserAsync(
            "workflow.validator",
            "workflow.validator@example.com",
            "Workflow Validator",
            Password,
            RoleCatalog.ExploitationControl); // has revenue.validate

        using var writerClient = await _factory.CreateAuthenticatedClientAsync("workflow.writer", Password);

        var createResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 1, 15),
                HotelUnitCode: hotelUnitCode,
                Accommodation: 1000m,
                Food: 200m,
                Beverage: 50m,
                Other: 10m,
                Notes: "Integration test entry"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<DailyRevenueResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(DailyRevenueStatus.Draft, created!.Status);

        var submitResponse = await writerClient.PostAsync($"/api/v1/revenue/daily/{created.Id}/submit", content: null);

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var submitted = await submitResponse.Content.ReadFromJsonAsync<DailyRevenueResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(submitted);
        Assert.Equal(DailyRevenueStatus.Submitted, submitted!.Status);

        using var validatorClient = await _factory.CreateAuthenticatedClientAsync("workflow.validator", Password);

        var validateResponse = await validatorClient.PostAsync($"/api/v1/revenue/daily/{created.Id}/validate", content: null);

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

        var validated = await validateResponse.Content.ReadFromJsonAsync<DailyRevenueResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(validated);
        Assert.Equal(DailyRevenueStatus.Validated, validated!.Status);
    }

    [Fact]
    public async Task Creating_daily_revenue_without_the_revenue_write_permission_returns_403()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("NOPHTL", "No Permission Hotel");

        await _factory.CreateUserAsync(
            "no.write.user",
            "no.write.user@example.com",
            "No Write User",
            Password,
            RoleCatalog.Reader); // reader has revenue.read but NOT revenue.write

        using var client = await _factory.CreateAuthenticatedClientAsync("no.write.user", Password);

        var response = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 1, 15),
                HotelUnitCode: hotelUnitCode,
                Accommodation: 1000m,
                Food: 200m,
                Beverage: 50m,
                Other: 10m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// L'ancien corps à quatre montants et le corps par lignes de catégories produisent la même
    /// recette : mêmes montants historiques, mêmes lignes, même total, et la synthèse expose les
    /// totaux par catégorie à côté des quatre montants. Envoyer les deux formes à la fois est refusé.
    /// </summary>
    [Fact]
    public async Task Legacy_four_amount_body_and_category_lines_body_produce_the_same_entry()
    {
        var legacyUnit = await _factory.CreateHotelUnitAsync("EQVLEG", "Equivalence Legacy Hotel");
        var linesUnit = await _factory.CreateHotelUnitAsync("EQVLIN", "Equivalence Lines Hotel");
        var businessDate = new DateOnly(2026, 2, 10);

        await _factory.CreateUserAsync(
            "equivalence.writer",
            "equivalence.writer@example.com",
            "Equivalence Writer",
            Password,
            RoleCatalog.UnitManager);

        using var client = await _factory.CreateAuthenticatedClientAsync("equivalence.writer", Password);

        var legacyResponse = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: businessDate,
                HotelUnitCode: legacyUnit,
                Accommodation: 1000m,
                Food: 200m,
                Beverage: 50m,
                Other: 10m),
            RaqmiApiFactory.JsonOptions);

        var linesResponse = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: businessDate,
                HotelUnitCode: linesUnit,
                Lines:
                [
                    new DailyRevenueLineRequest(RevenueCategoryCodes.Accommodation, 1000m),
                    new DailyRevenueLineRequest("food", 200m),
                    new DailyRevenueLineRequest(RevenueCategoryCodes.Beverage, 50m),
                    new DailyRevenueLineRequest(RevenueCategoryCodes.Other, 10m)
                ]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, legacyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, linesResponse.StatusCode);

        var legacy = await legacyResponse.Content.ReadFromJsonAsync<DailyRevenueResponse>(RaqmiApiFactory.JsonOptions);
        var byLines = await linesResponse.Content.ReadFromJsonAsync<DailyRevenueResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(legacy);
        Assert.NotNull(byLines);

        foreach (var entry in new[] { legacy!, byLines! })
        {
            Assert.Equal(1000m, entry.Accommodation);
            Assert.Equal(200m, entry.Food);
            Assert.Equal(50m, entry.Beverage);
            Assert.Equal(10m, entry.Other);
            Assert.Equal(1260m, entry.Total);

            Assert.Equal(
                [
                    (RevenueCategoryCodes.Accommodation, "Hébergement", 1000m),
                    (RevenueCategoryCodes.Food, "Restauration", 200m),
                    (RevenueCategoryCodes.Beverage, "Bar", 50m),
                    (RevenueCategoryCodes.Other, "Autres", 10m)
                ],
                entry.Lines.Select(line => (line.CategoryCode, line.CategoryLabel, line.Amount)));
        }

        var summary = await client.GetFromJsonAsync<DailyRevenueSummaryResponse>(
            $"/api/v1/revenue/daily/summary?from={businessDate:yyyy-MM-dd}&to={businessDate:yyyy-MM-dd}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal(2, summary!.EntryCount);
        Assert.Equal(2000m, summary.Accommodation);
        Assert.Equal(2520m, summary.Total);
        Assert.Equal(
            [(RevenueCategoryCodes.Accommodation, 2000m), (RevenueCategoryCodes.Food, 400m), (RevenueCategoryCodes.Beverage, 100m), (RevenueCategoryCodes.Other, 20m)],
            summary.Categories.Select(total => (total.CategoryCode, total.Amount)));

        var mixed = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: businessDate.AddDays(1),
                HotelUnitCode: linesUnit,
                Accommodation: 5m,
                Lines: [new DailyRevenueLineRequest(RevenueCategoryCodes.Food, 1m)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
    }

    [Fact]
    public async Task Unknown_or_inactive_revenue_category_is_refused_with_400()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("CATUNK", "Unknown Category Hotel");

        await _factory.CreateUserAsync(
            "category.writer",
            "category.writer@example.com",
            "Category Writer",
            Password,
            RoleCatalog.UnitManager);

        using var client = await _factory.CreateAuthenticatedClientAsync("category.writer", Password);

        var unknown = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 3, 1),
                HotelUnitCode: hotelUnitCode,
                Lines: [new DailyRevenueLineRequest("NOSUCHCAT", 100m)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("NOSUCHCAT", await unknown.Content.ReadAsStringAsync());

        // Une catégorie désactivée ne reçoit plus de nouvelle recette.
        var created = await client.PostAsJsonAsync(
            "/api/v1/revenue/categories",
            new CreateRevenueCategoryRequest("TMPCAT", "Catégorie temporaire", 90, BusinessSector.Hospitality),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var deactivated = await client.PutAsJsonAsync(
            "/api/v1/revenue/categories/TMPCAT",
            new UpdateRevenueCategoryRequest("Catégorie temporaire", 90, BusinessSector.Hospitality, IsActive: false),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var inactive = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 3, 1),
                HotelUnitCode: hotelUnitCode,
                Lines: [new DailyRevenueLineRequest("TMPCAT", 100m)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Contains("inactive", await inactive.Content.ReadAsStringAsync());

        // Un montant négatif reste refusé, quelle que soit la forme du corps.
        var negative = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 3, 1),
                HotelUnitCode: hotelUnitCode,
                Lines: [new DailyRevenueLineRequest(RevenueCategoryCodes.Food, -1m)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    /// <summary>
    /// Les catégories se listent par secteur ou par unité (jeu dédié, sinon jeu générique) avec
    /// revenue.read, et se paramètrent avec revenue.write - aucune clé nouvelle. Une catégorie
    /// référencée par une recette ne se supprime pas : elle se désactive.
    /// </summary>
    [Fact]
    public async Task Revenue_categories_are_listed_per_sector_and_managed_with_revenue_write()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("CATHTL", "Categories Hotel");

        await _factory.CreateUserAsync(
            "category.manager",
            "category.manager@example.com",
            "Category Manager",
            Password,
            RoleCatalog.UnitManager);

        await _factory.CreateUserAsync(
            "category.reader",
            "category.reader@example.com",
            "Category Reader",
            Password,
            RoleCatalog.Reader);

        using var writer = await _factory.CreateAuthenticatedClientAsync("category.manager", Password);
        using var reader = await _factory.CreateAuthenticatedClientAsync("category.reader", Password);

        var hotelSet = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            "/api/v1/revenue/categories?sector=Hospitality",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(
            [RevenueCategoryCodes.Accommodation, RevenueCategoryCodes.Food, RevenueCategoryCodes.Beverage, RevenueCategoryCodes.Other],
            hotelSet!.Select(category => category.Code));

        var tradeSet = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            "/api/v1/revenue/categories?sector=Trade",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(
            [RevenueCategoryCodes.Merchandise, RevenueCategoryCodes.Services, RevenueCategoryCodes.OtherIncome],
            tradeSet!.Select(category => category.Code));

        var unitSet = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            $"/api/v1/revenue/categories?hotelUnitCode={hotelUnitCode}",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(hotelSet!.Select(category => category.Code), unitSet!.Select(category => category.Code));

        var badSector = await reader.GetAsync("/api/v1/revenue/categories?sector=Bakery");
        Assert.Equal(HttpStatusCode.BadRequest, badSector.StatusCode);

        var unknownUnit = await reader.GetAsync("/api/v1/revenue/categories?hotelUnitCode=NOSUCHUNIT");
        Assert.Equal(HttpStatusCode.NotFound, unknownUnit.StatusCode);

        // Lire suffit à consulter, pas à paramétrer.
        var forbidden = await reader.PostAsJsonAsync(
            "/api/v1/revenue/categories",
            new CreateRevenueCategoryRequest("SPA", "Spa", 35, BusinessSector.Hospitality),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var created = await writer.PostAsJsonAsync(
            "/api/v1/revenue/categories",
            new CreateRevenueCategoryRequest(" spa ", "Spa et bien-être", 35, BusinessSector.Hospitality),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var spa = await created.Content.ReadFromJsonAsync<RevenueCategoryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(spa);
        Assert.Equal("SPA", spa!.Code);
        Assert.Equal(BusinessSector.Hospitality, spa.Sector);
        Assert.True(spa.IsActive);

        var duplicate = await writer.PostAsJsonAsync(
            "/api/v1/revenue/categories",
            new CreateRevenueCategoryRequest("SPA", "Doublon", 1),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // La nouvelle catégorie prend sa place dans le jeu de l'hôtel, à son rang d'affichage.
        var withSpa = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            $"/api/v1/revenue/categories?hotelUnitCode={hotelUnitCode}",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(
            [RevenueCategoryCodes.Accommodation, RevenueCategoryCodes.Food, RevenueCategoryCodes.Beverage, "SPA", RevenueCategoryCodes.Other],
            withSpa!.Select(category => category.Code));

        // Désactivée, elle disparaît des listes de saisie mais reste visible à l'administration.
        var updated = await writer.PutAsJsonAsync(
            "/api/v1/revenue/categories/spa",
            new UpdateRevenueCategoryRequest("Spa", 35, BusinessSector.Hospitality, IsActive: false),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var activeOnly = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            $"/api/v1/revenue/categories?hotelUnitCode={hotelUnitCode}",
            RaqmiApiFactory.JsonOptions);

        Assert.DoesNotContain(activeOnly!, category => category.Code == "SPA");

        var all = await reader.GetFromJsonAsync<IReadOnlyCollection<RevenueCategoryResponse>>(
            "/api/v1/revenue/categories?includeInactive=true",
            RaqmiApiFactory.JsonOptions);

        var inactiveSpa = Assert.Single(all!, category => category.Code == "SPA");
        Assert.False(inactiveSpa.IsActive);
        Assert.Equal("Spa", inactiveSpa.Label);

        // Sans référence, la suppression est possible ; référencée, elle est refusée.
        var deleted = await writer.DeleteAsync("/api/v1/revenue/categories/SPA");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await reader.GetAsync("/api/v1/revenue/categories/SPA");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var referenced = await writer.PostAsJsonAsync(
            "/api/v1/revenue/categories",
            new CreateRevenueCategoryRequest("REFCAT", "Catégorie référencée", 36, BusinessSector.Hospitality),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, referenced.StatusCode);

        var revenue = await writer.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(
                BusinessDate: new DateOnly(2026, 4, 1),
                HotelUnitCode: hotelUnitCode,
                Lines: [new DailyRevenueLineRequest("REFCAT", 12m)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, revenue.StatusCode);

        var refusedDelete = await writer.DeleteAsync("/api/v1/revenue/categories/REFCAT");
        Assert.Equal(HttpStatusCode.Conflict, refusedDelete.StatusCode);
    }
}
