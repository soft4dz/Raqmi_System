using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the login and "current user" endpoints: real routing, real
/// JSON model binding, and the real JwtBearer authentication middleware end to end (as opposed to
/// AuthenticationServiceTests.cs, which exercises AuthenticationService directly without going
/// through HTTP).
///
/// All tests in this class share one RaqmiApiFactory (and therefore one in-memory database), so
/// each test creates its own uniquely-named user to avoid interfering with the others - the
/// lockout test in particular must not share an account with any other test.
/// </summary>
public sealed class AuthenticationEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public AuthenticationEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_with_tokens_and_role_permissions()
    {
        await _factory.CreateUserAsync(
            "cashier.valid",
            "cashier.valid@example.com",
            "Cashier Valid",
            Password,
            RoleCatalog.Cashier);

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("cashier.valid", Password),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("cashier.valid", body.User.UserName);
        Assert.Contains(RoleCatalog.Cashier, body.User.Roles);

        var expectedPermissions = new[]
        {
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.TreasuryWrite,
            PermissionCatalog.SettingsRead,
            PermissionCatalog.LodgingRead,
            PermissionCatalog.LodgingCheckin,
            // Module 10 : les gestes de comptoir du PMS. La reception vend, affecte, enregistre
            // arrivees et departs, deplace un client de chambre, constate un no-show, annule et
            // passe le night audit de sa nuit.
            //
            // Elle n'a PAS lodging.change_rate, lodging.override_restriction, lodging.overbooking,
            // lodging.manage_rooms ni lodging.manage_rates : ces cinq cles engagent au-dela de la
            // nuit en cours - changer un prix vendu, lever une fermeture decidee ailleurs, vendre
            // une chambre qui n'existe pas, reecrire le parametrage.
            PermissionCatalog.LodgingReserve,
            PermissionCatalog.LodgingCheckout,
            PermissionCatalog.LodgingRoomMove,
            PermissionCatalog.LodgingNoShow,
            PermissionCatalog.LodgingCancel,
            PermissionCatalog.LodgingNightAudit,
            // Module 10.2: the front desk posts minibar consumption onto a folio at check-out,
            // which is a housekeeping write. It never gets housekeeping.inspect - signing a room
            // off is the floor supervisor act.
            PermissionCatalog.HousekeepingRead,
            PermissionCatalog.HousekeepingWrite,
            // Module 10.4: the front desk is where the relationship is actually recorded - the
            // opt-in collected at check-in, a room preference, the call taken this morning. It
            // never gets crm.loyalty: moving points is redeemable value.
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite,
            // Wave C: a cashier consults the validation circuits their documents go through,
            // but never configures one (approvals.write) and never decides (approvals.decide).
            PermissionCatalog.ApprovalsRead
            // Wave E1 (stocks, achats, cuisine) grants the front desk NOTHING: the store, the
            // ordering and the kitchen are not front-office acts.
        };

        Assert.Equal(expectedPermissions.Order(), body.User.Permissions.Order());
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await _factory.CreateUserAsync(
            "wrongpass.user",
            "wrongpass.user@example.com",
            "Wrong Pass User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("wrongpass.user", "definitely-not-the-password"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_locks_out_after_five_failed_attempts_and_then_rejects_the_correct_password_too()
    {
        await _factory.CreateUserAsync(
            "lockout.user",
            "lockout.user@example.com",
            "Lockout User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest("lockout.user", "still-not-the-password"),
                RaqmiApiFactory.JsonOptions);

            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        // The account is now locked out - even the correct password must be rejected. This
        // proves the lockout is enforced by the real HTTP pipeline, not only inside the service.
        var responseWithCorrectPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("lockout.user", Password),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, responseWithCorrectPassword.StatusCode);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_a_valid_token_returns_200_with_the_expected_claims()
    {
        await _factory.CreateUserAsync(
            "me.user",
            "me.user@example.com",
            "Me User",
            Password,
            RoleCatalog.Direction);

        using var client = await _factory.CreateAuthenticatedClientAsync("me.user", Password);

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.Equal("me.user", body!.UserName);
        Assert.Equal("me.user@example.com", body.Email);
        Assert.Contains(RoleCatalog.Direction, body.Roles);

        var expectedPermissions = new[]
        {
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.AuditRead,
            PermissionCatalog.ReportsExport,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.TreasuryApprove,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.SettingsRead,
            PermissionCatalog.AccountingRead,
            PermissionCatalog.BudgetRead,
            PermissionCatalog.BudgetApprove,
            PermissionCatalog.ReceivablesRead,
            PermissionCatalog.TariffsRead,
            PermissionCatalog.LodgingRead,
            // Module 10.2: direction reads the housekeeping board but never runs it - planning a
            // sheet and signing off a room are unit-level acts.
            PermissionCatalog.HousekeepingRead,
            // Wave C. Direction reads and DECIDES validations (it is a decider role) but does not
            // configure circuits - approvals.write belongs to exploitation.control. It reads the
            // reports and the backup state; maintenance.backup is deliberately absent, since only
            // system.administrator holds it through the catch-all grant of PermissionCatalog.All.
            // Module 10.4: direction reads the CRM and writes none of it - qualifying a guest
            // and moving their points are unit-level acts.
            PermissionCatalog.CrmRead,
            PermissionCatalog.ApprovalsRead,
            PermissionCatalog.ApprovalsDecide,
            PermissionCatalog.ReportsRead,
            PermissionCatalog.MaintenanceRead,
            // Module 21: direction reads the HR module - headcount, contracts, payroll totals -
            // and writes none of it; hr.write and the payroll keys stay with the HR profile.
            PermissionCatalog.HrRead,
            // Wave E1: direction reads the three operating modules and holds exactly the two
            // acts that engage the establishment rather than run it - closing a physical count
            // (it writes the adjustment movements) and approving a purchase order (it commits
            // the spend). inventory.write, purchasing.write, purchasing.receive and
            // kitchen.write are deliberately absent.
            PermissionCatalog.InventoryRead,
            PermissionCatalog.InventoryValidate,
            PermissionCatalog.PurchasingRead,
            PermissionCatalog.PurchasingApprove,
            PermissionCatalog.KitchenRead,
            // Module 10.6: direction reads the events, the quotes and the group blocks, and
            // writes none of it - selling a seminar or holding a block of rooms are unit-level
            // acts. mice.write is deliberately absent.
            PermissionCatalog.MiceRead,
            // Module 29: same reasoning as maintenance.read - direction checks that the fleet is
            // up to date and that no workstation runs a stale build, without going through the
            // system administrator.
            PermissionCatalog.SyncRead,
            // Bibliotheque KPI: reading indicators needs NO new key (dashboard.read plus the
            // source-module keys above), but setting the alert thresholds, mapping the chart of
            // accounts onto the GOP groups and closing a snapshot are governance acts - the same
            // family as budget.approve, so the key sits with direction.
            PermissionCatalog.KpiAdmin
        };

        Assert.Equal(expectedPermissions.Order(), body.Permissions.Order());
    }

    private sealed record MeResponse(
        string Id,
        string UserName,
        string? Email,
        string[] Roles,
        string[] Permissions);
}
