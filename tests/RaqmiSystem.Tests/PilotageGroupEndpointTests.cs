using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the CEO dashboard endpoint. The dashboard is protected by
/// the EXISTING dashboard.read permission (no new key): the tests grant it to a dedicated role
/// as a literal string, so they exercise the real per-permission authorization policy.
///
/// The scenario deliberately plants, next to each figure that must be counted, a row of the
/// same family that must NOT be (a submitted revenue, a draft receipt, a cancelled stay): the
/// end-to-end assertion is therefore that the EF query and the calculator TOGETHER apply the
/// owning modules' counting rules, not merely that the route answers.
/// </summary>
public sealed class PilotageGroupEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";
    private const string DashboardRead = "dashboard.read";

    private readonly RaqmiApiFactory _factory;

    public PilotageGroupEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Group_dashboard_aggregates_every_module_under_its_own_counting_rule()
    {
        // A period entirely in the past, so every one of its days can be reproached for not
        // being closed, and far enough back that no other test's data can drift into it.
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 4, 3);
        var utcNow = DateTimeOffset.UtcNow;

        var unitCode = await _factory.CreateHotelUnitAsync("PDGHTL", "Hotel du Groupe");

        await CreateRoleWithPermissionsAsync("pdg.dashboard.reader.role", DashboardRead);
        await _factory.CreateUserAsync(
            "pdg.dashboard.reader",
            "pdg.dashboard.reader@example.com",
            "Pdg Dashboard Reader",
            Password,
            "pdg.dashboard.reader.role");

        Guid roomId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

            // --- Revenue: only the VALIDATED entry is realised revenue. -------------------
            var validated = new DailyRevenue(from, unitCode, 800m, 150m, 50m, 0m);
            validated.Submit("pdg.dashboard.reader", utcNow.AddDays(-5));
            validated.Validate("pdg.dashboard.reader", utcNow.AddDays(-4));
            dbContext.Set<DailyRevenue>().Add(validated);

            // Submitted four days ago: excluded from the revenue, and past the 48-hour wait,
            // so it is exactly what the pending-validation alert reports.
            var submitted = new DailyRevenue(from.AddDays(1), unitCode, 400m, 0m, 0m, 0m);
            submitted.Submit("pdg.dashboard.reader", utcNow.AddDays(-4));
            dbContext.Set<DailyRevenue>().Add(submitted);

            // A draft: an uncontrolled keystroke, counted by nothing.
            dbContext.Set<DailyRevenue>().Add(new DailyRevenue(to, unitCode, 999m, 0m, 0m, 0m));

            // --- Receipts: only the CONFIRMED one is money in. ----------------------------
            var confirmed = new CashReceipt(from, unitCode, PaymentMethod.Cash, 620m);
            confirmed.Confirm("pdg.dashboard.reader", utcNow.AddDays(-4));
            dbContext.Set<CashReceipt>().Add(confirmed);

            dbContext.Set<CashReceipt>().Add(new CashReceipt(from.AddDays(1), unitCode, PaymentMethod.Cash, 300m));

            // --- Receivables: only the ISSUED invoice is still owed. ----------------------
            dbContext.Set<Customer>().Add(new Customer("PDG-CLI", "Client du Groupe", CustomerType.Company));

            var issued = new Invoice("PDG-CLI", unitCode, from.AddDays(-70));
            issued.ReplaceLines([new InvoiceLine("Séjour groupe", 1m, 1500m, 0m)]);
            issued.CaptureCustomerSnapshot("Client du Groupe", "1", null, null, null, null);
            issued.CaptureIssuerSnapshot("Hotel du Groupe", "2", null, null, null, null);
            issued.Issue(2026, 1, "pdg.dashboard.reader", utcNow.AddDays(-70));
            dbContext.Set<Invoice>().Add(issued);

            var paid = new Invoice("PDG-CLI", unitCode, from);
            paid.ReplaceLines([new InvoiceLine("Séjour réglé", 1m, 900m, 0m)]);
            paid.CaptureCustomerSnapshot("Client du Groupe", "1", null, null, null, null);
            paid.CaptureIssuerSnapshot("Hotel du Groupe", "2", null, null, null, null);
            paid.Issue(2026, 2, "pdg.dashboard.reader", utcNow.AddDays(-3));
            paid.MarkPaid("pdg.dashboard.reader", utcNow.AddDays(-2));
            dbContext.Set<Invoice>().Add(paid);

            // --- Occupancy: one active room, one blocking stay, one cancelled stay. -------
            dbContext.Set<RoomType>().Add(new RoomType(unitCode, "STD", "Standard", 2));

            var room = new Room(unitCode, "101", "STD");
            dbContext.Set<Room>().Add(room);
            roomId = room.Id;

            // CheckedOut still blocks - those nights were really consumed. Nights of 1 and 2
            // April (the departure night is not part of the stay).
            var stay = new Reservation(unitCode, room.Id, "PDG-CLI", from, to, 2, 100m, "STD-PLAN");
            stay.CheckIn(to, "pdg.dashboard.reader", utcNow.AddDays(-3));
            stay.CheckOut("pdg.dashboard.reader", utcNow.AddDays(-1));
            dbContext.Set<Reservation>().Add(stay);

            // --- Closing: the first day only, so two of the three past days are late. -----
            dbContext.Set<DailyClosing>().Add(new DailyClosing(from, unitCode, "pdg.dashboard.reader", utcNow.AddDays(-4)));

            // --- Budget: an APPROVED plan; only April is touched by the period. -----------
            var plan = new BudgetPlan(2026, unitCode, "Budget 2026");
            plan.SetLine(4, BudgetCategory.Accommodation, 700m);
            plan.SetLine(5, BudgetCategory.Accommodation, 9000m);
            plan.Approve("pdg.dashboard.reader", utcNow.AddDays(-30));
            dbContext.Set<BudgetPlan>().Add(plan);

            await dbContext.SaveChangesAsync();
        }

        using var client = await _factory.CreateAuthenticatedClientAsync("pdg.dashboard.reader", Password);

        var response = await client.GetAsync(
            $"/api/v1/pilotage/group-dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var dashboard = await response.Content.ReadFromJsonAsync<GroupDashboardResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(dashboard);

        Assert.Equal(from, dashboard!.From);
        Assert.Equal(to, dashboard.To);
        Assert.Equal(new DateOnly(2025, 4, 1), dashboard.PreviousFrom);
        Assert.Equal(new DateOnly(2025, 4, 3), dashboard.PreviousTo);

        // Group KPIs: the validated 1000, the confirmed 620, the issued 1500 - and nothing else.
        Assert.Equal(1000m, dashboard.Kpis.ValidatedRevenue);
        Assert.Equal(620m, dashboard.Kpis.ConfirmedReceipts);
        Assert.Equal(1500m, dashboard.Kpis.OutstandingReceivables);
        Assert.Equal(1, dashboard.Kpis.OutstandingInvoiceCount);

        // Two nights busy out of one active room over three days.
        Assert.Equal(2, dashboard.Kpis.OccupiedNights);
        Assert.Equal(3, dashboard.Kpis.AvailableNights);
        Assert.Equal(66.67m, dashboard.Kpis.OccupancyRatePercent);
        Assert.True(dashboard.Kpis.ActiveUnitCount >= 1);

        // Nothing a year earlier: every variation is a dash, never a zero.
        Assert.Null(dashboard.Variations.RevenuePercent);
        Assert.Null(dashboard.Variations.ReceiptsPercent);
        Assert.Null(dashboard.Variations.ReceivablesPercent);
        Assert.Null(dashboard.Variations.OccupancyPercent);

        var row = Assert.Single(dashboard.Units, unit => unit.HotelUnitCode == unitCode);
        Assert.Equal("Hotel du Groupe", row.HotelUnitName);
        Assert.Equal(1000m, row.ValidatedRevenue);
        Assert.Equal(620m, row.ConfirmedReceipts);
        Assert.Equal(100m, row.GroupSharePercent);
        Assert.Equal(2, row.OccupiedNights);
        Assert.Equal(66.67m, row.OccupancyRatePercent);

        // The 1st is closed; the 2nd and the 3rd are not.
        Assert.Equal(2, row.UnclosedDayCount);

        // Only April's target is in scope, and the plan is approved so it is a reference.
        Assert.Equal(700m, row.BudgetTarget);
        Assert.Equal(300m, row.BudgetVarianceAmount);

        var unclosed = Assert.Single(
            dashboard.Alerts,
            alert => alert.Type == GroupAlertType.UnclosedDays && alert.HotelUnitCode == unitCode);
        Assert.Equal(GroupAlertSeverity.Attention, unclosed.Severity);
        Assert.Equal(2, unclosed.Count);

        var pending = Assert.Single(
            dashboard.Alerts,
            alert => alert.Type == GroupAlertType.PendingValidation && alert.HotelUnitCode == unitCode);
        Assert.Equal(GroupAlertSeverity.Info, pending.Severity);
        Assert.Equal(1, pending.Count);

        // The invoice is 70 days old at the period's end: the aging module's 61-90 bracket.
        var overdue = Assert.Single(
            dashboard.Alerts,
            alert => alert.Type == GroupAlertType.OverdueInvoices && alert.HotelUnitCode == unitCode);
        Assert.Equal(1, overdue.Count);

        // The payload says, in the server's own words, what each family of figures counts.
        Assert.False(string.IsNullOrWhiteSpace(dashboard.Basis.Revenue));
        Assert.False(string.IsNullOrWhiteSpace(dashboard.Basis.Occupancy));

        Assert.NotEqual(Guid.Empty, roomId);
    }

    [Fact]
    public async Task Group_dashboard_is_forbidden_without_dashboard_read()
    {
        await CreateRoleWithPermissionsAsync("pdg.dashboard.norole.role", "revenue.read");
        await _factory.CreateUserAsync(
            "pdg.dashboard.norole",
            "pdg.dashboard.norole@example.com",
            "Pdg Dashboard No Role",
            Password,
            "pdg.dashboard.norole.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("pdg.dashboard.norole", Password);

        var response = await client.GetAsync("/api/v1/pilotage/group-dashboard?from=2026-04-01&to=2026-04-03");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Group_dashboard_refuses_an_inverted_period()
    {
        await CreateRoleWithPermissionsAsync("pdg.dashboard.bounds.role", DashboardRead);
        await _factory.CreateUserAsync(
            "pdg.dashboard.bounds",
            "pdg.dashboard.bounds@example.com",
            "Pdg Dashboard Bounds",
            Password,
            "pdg.dashboard.bounds.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("pdg.dashboard.bounds", Password);

        var response = await client.GetAsync("/api/v1/pilotage/group-dashboard?from=2026-04-30&to=2026-04-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Group_dashboard_refuses_a_window_longer_than_a_year()
    {
        await CreateRoleWithPermissionsAsync("pdg.dashboard.window.role", DashboardRead);
        await _factory.CreateUserAsync(
            "pdg.dashboard.window",
            "pdg.dashboard.window@example.com",
            "Pdg Dashboard Window",
            Password,
            "pdg.dashboard.window.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("pdg.dashboard.window", Password);

        // 367 days: one day past the cap the lodging module already applies to occupancy, which
        // this dashboard computes day by day in memory.
        var response = await client.GetAsync("/api/v1/pilotage/group-dashboard?from=2025-01-01&to=2026-01-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Group_dashboard_requires_both_bounds()
    {
        await CreateRoleWithPermissionsAsync("pdg.dashboard.missing.role", DashboardRead);
        await _factory.CreateUserAsync(
            "pdg.dashboard.missing",
            "pdg.dashboard.missing@example.com",
            "Pdg Dashboard Missing",
            Password,
            "pdg.dashboard.missing.role");

        using var client = await _factory.CreateAuthenticatedClientAsync("pdg.dashboard.missing", Password);

        var response = await client.GetAsync("/api/v1/pilotage/group-dashboard?from=2026-04-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Creates (or completes) a dedicated test role granting the given permission keys as
    /// literal strings, so the tests only depend on the keys and on the authorization policies
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
            role = new Role(roleName, roleName, "Group dashboard integration test role.");
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
                    "Group dashboard integration test permission.");

                dbContext.Permissions.Add(permission);
            }

            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync();
    }
}
