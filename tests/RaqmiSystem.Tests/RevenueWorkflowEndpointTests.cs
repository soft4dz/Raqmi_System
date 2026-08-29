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
}
