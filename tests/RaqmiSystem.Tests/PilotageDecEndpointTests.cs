using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the DEC cockpit endpoint. The cockpit is protected by
/// the EXISTING dashboard.read permission (no new key): the tests grant it to a dedicated role
/// as a literal string, so they exercise the real per-permission authorization policy.
/// </summary>
public sealed class PilotageDecEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";
    private const string DashboardRead = "dashboard.read";

    private readonly RaqmiApiFactory _factory;

    public PilotageDecEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cockpit_reports_work_queues_and_unit_health_under_dashboard_read()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var utcNow = DateTimeOffset.UtcNow;

        var hotelUnitCode = await _factory.CreateHotelUnitAsync("DECHTL", "Cockpit Hotel");

        await CreateRoleWithPermissionsAsync("dec.cockpit.reader.role", DashboardRead);
        await _factory.CreateUserAsync(
            "dec.cockpit.reader",
            "dec.cockpit.reader@example.com",
            "Dec Cockpit Reader",
            Password,
            "dec.cockpit.reader.role");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            // Yesterday's revenue, SUBMITTED three days' worth of hours ago: the validation
            // queue must report one entry of 650 with an age of exactly 3 whole days.
            var submitted = new DailyRevenue(yesterday, hotelUnitCode, 500m, 100m, 50m, 0m);
            submitted.Submit("dec.cockpit.reader", utcNow.AddDays(-3));
            dbContext.Set<DailyRevenue>().Add(submitted);

            // A REJECTED entry awaiting correction (Status stays Rejected until the unit
            // edits it - the mechanic the cockpit relies on).
            var rejectedEntry = new DailyRevenue(today.AddDays(-3), hotelUnitCode, 999m, 0m, 0m, 0m);
            rejectedEntry.Submit("dec.cockpit.reader", utcNow.AddDays(-3));
            rejectedEntry.Reject("Montant a revoir.", "dec.cockpit.reader", utcNow.AddDays(-2));
            dbContext.Set<DailyRevenue>().Add(rejectedEntry);

            // A Draft payment order awaiting approval, dated four days ago.
            dbContext.Set<BankAccount>().Add(new BankAccount("DEC-BNA", "Compte BNA", "BNA", "0012345678901234"));
            dbContext.Set<PaymentOrder>().Add(new PaymentOrder(
                today.AddDays(-4),
                "Imprimerie du Port",
                1200m,
                today,
                "DEC-BNA"));

            await dbContext.SaveChangesAsync();
        }

        using var client = await _factory.CreateAuthenticatedClientAsync("dec.cockpit.reader", Password);

        var response = await client.GetAsync($"/api/v1/pilotage/dec-cockpit?date={today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cockpit = await response.Content.ReadFromJsonAsync<DecCockpitResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(cockpit);

        Assert.Equal(today, cockpit!.Date);
        Assert.Equal(yesterday, cockpit.Yesterday);

        // Queue 1: submitted revenue awaiting validation.
        Assert.Equal(1, cockpit.PendingValidationCount);
        Assert.Equal(650m, cockpit.PendingValidationAmount);
        var pending = Assert.Single(cockpit.PendingValidations);
        Assert.Equal(hotelUnitCode, pending.HotelUnitCode);
        Assert.Equal("Cockpit Hotel", pending.HotelUnitName);
        Assert.Equal(yesterday, pending.OldestBusinessDate);
        Assert.Equal(3, pending.OldestAgeDays);

        // Queue 2: closing backlog. First recorded activity is today-3 (the rejected entry's
        // business date) and nothing is closed, so today-3 and today-2 are late - never
        // yesterday, which is today's normal work.
        var backlogUnit = Assert.Single(cockpit.ClosingBacklog);
        Assert.Equal(hotelUnitCode, backlogUnit.HotelUnitCode);
        Assert.Equal([today.AddDays(-3), today.AddDays(-2)], backlogUnit.MissingDates);
        Assert.Equal(2, cockpit.ClosingBacklogDayCount);
        Assert.NotNull(cockpit.OldestClosingDelay);
        Assert.Equal(today.AddDays(-3), cockpit.OldestClosingDelay!.BusinessDate);
        Assert.Equal(3, cockpit.OldestClosingDelay.AgeDays);

        // Queue 3: rejected awaiting correction.
        var rejected = Assert.Single(cockpit.RejectedRevenues);
        Assert.Equal("Montant a revoir.", rejected.RejectionReason);
        Assert.Equal(999m, rejected.Total);
        Assert.Equal(2, rejected.AgeDays);

        // Queue 4: draft payment orders.
        var order = Assert.Single(cockpit.PendingPaymentOrders);
        Assert.Equal("Imprimerie du Port", order.Beneficiary);
        Assert.Equal(1200m, order.Amount);
        Assert.Equal(4, order.AgeDays);
        Assert.Equal(1200m, cockpit.PendingPaymentOrderAmount);

        // Unit health: yesterday's figure exists but is only SUBMITTED - usable, flagged as
        // provisional, and the unit therefore needs no attention highlight.
        var health = Assert.Single(cockpit.UnitHealth);
        Assert.Equal(hotelUnitCode, health.HotelUnitCode);
        Assert.Equal(DailyRevenueStatus.Submitted, health.YesterdayRevenueStatus);
        Assert.Equal(650m, health.YesterdayRevenueTotal);
        Assert.True(health.YesterdayRevenueIsProvisional);
        Assert.False(health.YesterdayClosed);
        Assert.False(health.NeedsAttention);
        Assert.Equal(0, health.ActiveRooms);
        Assert.Null(health.OccupancyRatePercent);
    }

    [Fact]
    public async Task Cockpit_is_forbidden_without_dashboard_read()
    {
        await CreateRoleWithPermissionsAsync("dec.cockpit.norole.role", "revenue.read");
        await _factory.CreateUserAsync(
            "dec.cockpit.norole",
            "dec.cockpit.norole@example.com",
            "Dec Cockpit No Role",
            Password,
            "dec.cockpit.norole.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("dec.cockpit.norole", Password);

        var response = await client.GetAsync("/api/v1/pilotage/dec-cockpit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cockpit_rejects_an_invalid_date()
    {
        await CreateRoleWithPermissionsAsync("dec.cockpit.date.role", DashboardRead);
        await _factory.CreateUserAsync(
            "dec.cockpit.date",
            "dec.cockpit.date@example.com",
            "Dec Cockpit Date",
            Password,
            "dec.cockpit.date.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("dec.cockpit.date", Password);

        var response = await client.GetAsync("/api/v1/pilotage/dec-cockpit?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Creates (or completes) a dedicated test role granting the given permission keys as
    /// literal strings, so the tests only depend on the keys and on the authorization
    /// policies Program.cs registers from PermissionCatalog.All.
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
            role = new Role(roleName, roleName, "DEC cockpit integration test role.");
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
                    "reporting",
                    "DEC cockpit integration test permission.");

                dbContext.Permissions.Add(permission);
            }

            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync();
    }
}
