using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Closing;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the daily-closing (night audit) workflow. The tests
/// create their own roles and grant them the closing permission keys as literal strings
/// ("closing.read" / "closing.close" / "closing.reopen"), so they exercise the real
/// per-permission authorization policies without depending on which system roles the
/// security seeder assigns those permissions to.
/// </summary>
public sealed class ClosingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string ClosingRead = "closing.read";
    private const string ClosingClose = "closing.close";
    private const string ClosingReopen = "closing.reopen";

    private readonly RaqmiApiFactory _factory;

    public ClosingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Closing_is_refused_while_a_submitted_revenue_exists()
    {
        var businessDate = new DateOnly(2026, 3, 10);
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("CLSHTL", "Closing Hotel");

        await CreateRoleWithPermissionsAsync("closing.pending.closer.role", ClosingRead, ClosingClose);
        await _factory.CreateUserAsync(
            "closing.pending.closer",
            "closing.pending.closer@example.com",
            "Closing Pending Closer",
            Password,
            "closing.pending.closer.role");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            var revenue = new DailyRevenue(businessDate, hotelUnitCode, 500m, 100m, 50m, 0m);
            revenue.Submit("closing.pending.closer", DateTimeOffset.UtcNow);

            dbContext.Set<DailyRevenue>().Add(revenue);
            await dbContext.SaveChangesAsync();
        }

        using var client = await _factory.CreateAuthenticatedClientAsync("closing.pending.closer", Password);

        var response = await client.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(businessDate, hotelUnitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Closed_day_can_be_reopened_only_by_a_role_with_the_reopen_permission()
    {
        var businessDate = new DateOnly(2026, 3, 11);
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("RPNHTL", "Reopen Hotel");

        await CreateRoleWithPermissionsAsync("closing.closer.role", ClosingRead, ClosingClose);
        await CreateRoleWithPermissionsAsync("closing.control.role", ClosingRead, ClosingReopen);

        await _factory.CreateUserAsync(
            "closing.closer",
            "closing.closer@example.com",
            "Closing Closer",
            Password,
            "closing.closer.role");

        await _factory.CreateUserAsync(
            "closing.controller",
            "closing.controller@example.com",
            "Closing Controller",
            Password,
            "closing.control.role");

        using var closerClient = await _factory.CreateAuthenticatedClientAsync("closing.closer", Password);

        var closeResponse = await closerClient.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(businessDate, hotelUnitCode, "Night audit done."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, closeResponse.StatusCode);

        var closed = await closeResponse.Content.ReadFromJsonAsync<DailyClosingResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(closed);
        Assert.Equal(ClosingStatus.Closed, closed!.Status);
        Assert.Equal(hotelUnitCode, closed.HotelUnitCode);

        // The closer role does not hold closing.reopen: reopening must be forbidden.
        var forbiddenReopen = await closerClient.PostAsJsonAsync(
            $"/api/v1/closing/daily/{closed.Id}/reopen",
            new ReopenDailyClosingRequest("Trying without the reopen permission."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReopen.StatusCode);

        using var controllerClient = await _factory.CreateAuthenticatedClientAsync("closing.controller", Password);

        var reopenResponse = await controllerClient.PostAsJsonAsync(
            $"/api/v1/closing/daily/{closed.Id}/reopen",
            new ReopenDailyClosingRequest("Ecart de caisse a corriger."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var reopened = await reopenResponse.Content.ReadFromJsonAsync<DailyClosingResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(reopened);
        Assert.Equal(ClosingStatus.Reopened, reopened!.Status);
        Assert.Equal("Ecart de caisse a corriger.", reopened.ReopenReason);

        // A reopened day can be closed again through the same close endpoint.
        var recloseResponse = await closerClient.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(businessDate, hotelUnitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, recloseResponse.StatusCode);

        var reclosed = await recloseResponse.Content.ReadFromJsonAsync<DailyClosingResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(reclosed);
        Assert.Equal(closed.Id, reclosed!.Id);
        Assert.Equal(ClosingStatus.Closed, reclosed.Status);
        Assert.Equal("Ecart de caisse a corriger.", reclosed.ReopenReason);

        // Closing an already-closed day is a conflict.
        var conflictResponse = await closerClient.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(businessDate, hotelUnitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task Closing_a_future_business_day_is_refused()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("FUTHTL", "Future Hotel");

        await CreateRoleWithPermissionsAsync("closing.future.role", ClosingRead, ClosingClose);
        await _factory.CreateUserAsync(
            "closing.future.closer",
            "closing.future.closer@example.com",
            "Closing Future Closer",
            Password,
            "closing.future.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("closing.future.closer", Password);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await client.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(tomorrow, hotelUnitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Closed_day_blocks_revenue_creation_update_and_submission()
    {
        var businessDate = new DateOnly(2026, 3, 12);
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("LCKHTL", "Locked Hotel");

        await CreateRoleWithPermissionsAsync(
            "closing.lock.role",
            ClosingRead,
            ClosingClose,
            "revenue.read",
            "revenue.write");

        await _factory.CreateUserAsync(
            "closing.locker",
            "closing.locker@example.com",
            "Closing Locker",
            Password,
            "closing.lock.role");

        // Seed a validated revenue entry for the day (a day with Draft/Submitted entries
        // cannot be closed, but a fully validated one can).
        Guid validatedRevenueId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            var revenue = new DailyRevenue(businessDate, hotelUnitCode, 800m, 200m, 100m, 0m);
            revenue.Submit("closing.locker", DateTimeOffset.UtcNow);
            revenue.Validate("closing.locker", DateTimeOffset.UtcNow);

            dbContext.Set<DailyRevenue>().Add(revenue);
            await dbContext.SaveChangesAsync();

            validatedRevenueId = revenue.Id;
        }

        using var client = await _factory.CreateAuthenticatedClientAsync("closing.locker", Password);

        var closeResponse = await client.PostAsJsonAsync(
            "/api/v1/closing/daily/close",
            new CloseBusinessDayRequest(businessDate, hotelUnitCode, "Night audit done."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, closeResponse.StatusCode);

        // Creating a revenue entry on the closed day+unit is refused.
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(businessDate, hotelUnitCode, 100m, 0m, 0m, 0m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        // Updating the existing revenue entry of the closed day is refused too.
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/revenue/daily/{validatedRevenueId}",
            new UpdateDailyRevenueRequest(999m, 0m, 0m, 0m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        // And so is any status transition (the closed-day guard runs before the status check).
        var submitResponse = await client.PostAsync(
            $"/api/v1/revenue/daily/{validatedRevenueId}/submit",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, submitResponse.StatusCode);
    }

    /// <summary>
    /// Creates (or completes) a dedicated test role granting the given permission keys.
    /// Permission rows are created on the fly when the catalog seeder has not inserted them,
    /// so the test only depends on the literal keys and the authorization policies that
    /// Program.cs registers from PermissionCatalog.All.
    /// </summary>
    private async Task CreateRoleWithPermissionsAsync(string roleName, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var role = await dbContext.Roles
            .Include(currentRole => currentRole.Permissions)
            .SingleOrDefaultAsync(currentRole => currentRole.Name == roleName);

        if (role is null)
        {
            role = new Role(roleName, roleName, "Closing integration test role.");
            dbContext.Roles.Add(role);
        }

        foreach (var permissionKey in permissionKeys)
        {
            var permission = await dbContext.Permissions
                .SingleOrDefaultAsync(currentPermission => currentPermission.Key == permissionKey);

            if (permission is null)
            {
                permission = new Permission(
                    permissionKey,
                    permissionKey,
                    "exploitation",
                    "Closing integration test permission.");

                dbContext.Permissions.Add(permission);
            }

            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync();
    }
}
