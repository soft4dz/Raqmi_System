using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Matrice RBAC par route (livrable 7, critere de sortie de la phase 2 : "absent / historique /
/// nouveau"), jouee en HTTP complet contre le vrai Program.cs - politiques d'autorisation
/// generees depuis le registre, JwtBearer, seeder. Chaque utilisateur porte un role dedie qui
/// ne detient QUE les cles du cas teste, si bien qu'un 403 ne peut venir que de la politique.
///
/// Trois verdicts par route retaguee : sans la cle -> 403 ; avec la cle HISTORIQUE -> la route
/// est atteinte (aucune perte d'acces) ; avec la cle CIBLE -> la route est atteinte. Sur une
/// route de lecture sans parametre, "atteinte" veut dire 200 ; sur une route qui engage un
/// objet, l'identifiant est factice et la preuve est un 404 ou un 400 - c'est le service qui
/// repond, la politique a laisse passer. Le dernier verdict est la regle d'or du registre : une
/// cle FINE seule ne vaut jamais la cle historique composite, ni sur une route restee sur la
/// cle historique (POST /security/users), ni sur une route retaguee vers une autre cle fine.
/// </summary>
public sealed class RbacPolicyMatrixTests : IClassFixture<RaqmiApiFactory>, IAsyncLifetime
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string HotelUnitCode = "RBACHT";

    /// <summary>Une cle sans rapport avec aucune route testee : le "sans la cle" n'est pas un utilisateur sans role.</summary>
    private const string UnrelatedKey = PermissionCatalog.SettingsRead;

    private readonly RaqmiApiFactory _factory;

    private string _hotelUnitCode = string.Empty;

    public RbacPolicyMatrixTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// xunit instancie la classe pour CHAQUE test : l'unite hoteliere que les routes PMS exigent
    /// est donc creee au plus une fois par fixture, pas une fois par test.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var existing = await dbContext.HotelUnits.SingleOrDefaultAsync(unit => unit.Code == HotelUnitCode);

        if (existing is null)
        {
            dbContext.HotelUnits.Add(new HotelUnit(HotelUnitCode, "RBAC Matrix Hotel", HotelUnitType.Hotel));
            await dbContext.SaveChangesAsync();
        }

        _hotelUnitCode = HotelUnitCode;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Une route de LECTURE par fichier retague (Finance, PMS, Achats/Stocks, RH) : 200 avec la
    /// cle historique comme avec la cle cible, 403 sans.
    /// </summary>
    public static TheoryData<string, string, string, string> ReadRoutes => new()
    {
        // fichier, route, cle historique, cle cible
        { "Accounting", "/api/v1/accounting/accounts", PermissionCatalog.AccountingRead, PermissionCatalog.FinanceAccountingRead },
        { "Treasury", "/api/v1/treasury/bank-accounts", PermissionCatalog.TreasuryRead, PermissionCatalog.FinanceTreasuryRead },
        { "Budget", "/api/v1/budget/plans", PermissionCatalog.BudgetRead, PermissionCatalog.FinanceBudgetRead },
        { "Receivables", "/api/v1/receivables/aging", PermissionCatalog.ReceivablesRead, PermissionCatalog.FinanceReceivableRead },
        { "Billing", "/api/v1/billing/customers", PermissionCatalog.CustomersRead, PermissionCatalog.CrmCustomerRead },
        { "Closing", "/api/v1/closing/daily", PermissionCatalog.ClosingRead, PermissionCatalog.LodgingClosingRead },
        { "Lodging", "/api/v1/lodging/room-types", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingFrontOfficeRead },
        { "LodgingCatalog", "/api/v1/lodging/extras?hotelUnitCode=RBACHT", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingFrontOfficeRead },
        { "LodgingInventory", "/api/v1/lodging/room-blocks?hotelUnitCode=RBACHT", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingFrontOfficeRead },
        { "LodgingOperations", "/api/v1/lodging/night-audit?hotelUnitCode=RBACHT", PermissionCatalog.LodgingRead, PermissionCatalog.LodgingFrontOfficeRead },
        { "Purchasing", "/api/v1/purchasing/suppliers", PermissionCatalog.PurchasingRead, PermissionCatalog.PurchasingOrderRead },
        { "Inventory", "/api/v1/inventory/warehouses", PermissionCatalog.InventoryRead, PermissionCatalog.InventoryStockRead },
        { "HumanResources", "/api/v1/hr/departments", PermissionCatalog.HrRead, PermissionCatalog.HrEmployeeRead }
    };

    [Theory]
    [MemberData(nameof(ReadRoutes))]
    public async Task A_retagged_read_route_accepts_the_legacy_key_and_the_target_key_and_refuses_neither_silently(
        string file,
        string route,
        string legacyKey,
        string targetKey)
    {
        var suffix = $"{file.ToLowerInvariant()}.{Guid.NewGuid():N}";

        using (var withoutKey = await LoginAsync($"rbac.none.{suffix}", UnrelatedKey))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await withoutKey.GetAsync(route)).StatusCode);
        }

        using (var withLegacyKey = await LoginAsync($"rbac.legacy.{suffix}", legacyKey))
        {
            Assert.Equal(HttpStatusCode.OK, (await withLegacyKey.GetAsync(route)).StatusCode);
        }

        using (var withTargetKey = await LoginAsync($"rbac.target.{suffix}", targetKey))
        {
            Assert.Equal(HttpStatusCode.OK, (await withTargetKey.GetAsync(route)).StatusCode);
        }
    }

    /// <summary>
    /// Les gestes qui ENGAGENT, retagues vers une cle fine : la cle historique (large ou fine)
    /// passe, la cle cible passe, une AUTRE cle fine du meme domaine - y compris une cle fine
    /// couverte par la meme cle historique composite - est refusee.
    /// </summary>
    public static TheoryData<string, string, string[], string[]> EngagingRoutes => new()
    {
        // route, corps JSON (vide = sans corps), cles qui passent, cles qui sont refusees
        {
            "/api/v1/accounting/entries/00000000-0000-0000-0000-000000000001/post", "",
            new[] { PermissionCatalog.AccountingPost, PermissionCatalog.FinanceEntryPost },
            new[] { PermissionCatalog.AccountingWrite, PermissionCatalog.FinanceEntryManage }
        },
        {
            "/api/v1/treasury/payment-orders/00000000-0000-0000-0000-000000000001/approve", "",
            new[] { PermissionCatalog.TreasuryApprove, PermissionCatalog.FinancePaymentOrderApprove },
            new[] { PermissionCatalog.TreasuryWrite, PermissionCatalog.FinancePaymentOrderManage }
        },
        {
            // treasury.write est composite : chacune de ses trois cles fines ouvre SA ressource.
            "/api/v1/treasury/bank-accounts/NOSUCH/deactivate", "",
            new[] { PermissionCatalog.TreasuryWrite, PermissionCatalog.FinanceBankAccountManage },
            new[] { PermissionCatalog.FinanceReceiptManage, PermissionCatalog.FinancePaymentOrderManage }
        },
        {
            "/api/v1/purchasing/orders/00000000-0000-0000-0000-000000000001/approve", "",
            new[] { PermissionCatalog.PurchasingApprove, PermissionCatalog.PurchasingOrderApprove },
            new[] { PermissionCatalog.PurchasingWrite, PermissionCatalog.PurchasingOrderManage }
        },
        {
            "/api/v1/inventory/counts/00000000-0000-0000-0000-000000000001/validate", "",
            new[] { PermissionCatalog.InventoryValidate, PermissionCatalog.InventoryCountValidate },
            new[] { PermissionCatalog.InventoryWrite, PermissionCatalog.InventoryCountManage }
        },
        {
            // hr.payroll.close etait deja au format cible : elle est sa propre cible, et ni la
            // preparation de la paie ni sa cle cible ne la valent.
            "/api/v1/hr/payroll/periods/2026-01/close", "",
            new[] { PermissionCatalog.HrPayrollClose },
            new[] { PermissionCatalog.HrPayroll, PermissionCatalog.HrPayrollProcess, PermissionCatalog.HrWrite }
        },
        {
            // lodging.checkin ("operer le comptoir") couvre l'arrivee ; la tenue des folios, qui
            // en est une autre cle fine, ne la vaut pas - et lodging.write, comme avant, non plus.
            "/api/v1/lodging/reservations/00000000-0000-0000-0000-000000000001/check-in", "",
            new[] { PermissionCatalog.LodgingCheckin, PermissionCatalog.LodgingCheckinExecute },
            new[] { PermissionCatalog.LodgingFolioManage, PermissionCatalog.LodgingWrite, PermissionCatalog.LodgingReservationCreate }
        },
        {
            // Les huit alias PMS, absorbes par le registre : lodging.write vaut toujours
            // lodging.manage_rooms, et la cle cible lodging.room.manage vaut les deux.
            "/api/v1/lodging/room-types", """{"hotelUnitCode":"RBACHT","code":"STE","name":"Suite","capacity":4}""",
            new[] { PermissionCatalog.LodgingWrite, PermissionCatalog.LodgingManageRooms, PermissionCatalog.LodgingRoomManage },
            new[] { PermissionCatalog.LodgingReserve, PermissionCatalog.LodgingReservationCreate, PermissionCatalog.LodgingCheckin }
        }
    };

    [Theory]
    [MemberData(nameof(EngagingRoutes))]
    public async Task A_retagged_engaging_route_is_reached_with_the_covering_keys_and_refused_with_a_sibling_fine_key(
        string route,
        string body,
        string[] acceptedKeys,
        string[] refusedKeys)
    {
        foreach (var key in acceptedKeys)
        {
            using var client = await LoginAsync($"rbac.ok.{Guid.NewGuid():N}", key);
            var response = await PostAsync(client, route, body);

            Assert.True(
                response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
                $"{key} devrait atteindre {route} ; recu {(int)response.StatusCode}.");
        }

        foreach (var key in refusedKeys)
        {
            using var client = await LoginAsync($"rbac.ko.{Guid.NewGuid():N}", key);
            var response = await PostAsync(client, route, body);

            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"{key} ne devrait PAS atteindre {route} ; recu {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task A_fine_key_alone_never_opens_a_route_still_tagged_with_its_composite_legacy_key()
    {
        // POST /security/users est reste sur users.write (le socle n'est pas dans le lot P0). Le
        // detenteur de la cle historique y accede ; le detenteur de la seule cle fine
        // admin.user.create, non - sinon elle vaudrait aussi la modification et la desactivation.
        using (var legacyHolder = await LoginAsync($"rbac.users.legacy.{Guid.NewGuid():N}"[..40], PermissionCatalog.UsersWrite))
        {
            var response = await legacyHolder.PostAsJsonAsync("/api/v1/security/users", new { }, RaqmiApiFactory.JsonOptions);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var fineHolder = await LoginAsync($"rbac.users.fine.{Guid.NewGuid():N}"[..40], PermissionCatalog.AdminUserCreate))
        {
            var response = await fineHolder.PostAsJsonAsync("/api/v1/security/users", new { }, RaqmiApiFactory.JsonOptions);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // Et la lecture 1:1 : users.read et admin.user.read se valent sur GET /security/users.
        using (var targetReader = await LoginAsync($"rbac.users.read.{Guid.NewGuid():N}"[..40], PermissionCatalog.AdminUserRead))
        {
            Assert.Equal(HttpStatusCode.OK, (await targetReader.GetAsync("/api/v1/security/users")).StatusCode);
        }
    }

    [Fact]
    public async Task The_optional_levers_accept_the_target_key_like_the_legacy_key()
    {
        // La surreservation est un levier optionnel evalue par HasPermission, pas par une
        // politique de route : la recherche repond toujours, mais n'ouvre l'inventaire etendu
        // qu'au detenteur de la cle - historique ou cible, avec la meme regle que les routes.
        foreach (var key in new[] { PermissionCatalog.LodgingOverbooking, PermissionCatalog.LodgingReservationOverbook })
        {
            using var client = await LoginAsync($"rbac.lever.{Guid.NewGuid():N}", PermissionCatalog.LodgingRead, key);

            var response = await client.GetAsync(
                $"/api/v1/lodging/availability?hotelUnitCode={_hotelUnitCode}&from=2030-05-01&to=2030-05-02&allowOverbooking=true");

            Assert.True(
                response.StatusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.Unauthorized,
                $"{key} : recu {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task The_migration_report_lists_custom_roles_with_their_missing_target_keys_and_is_read_with_roles_read()
    {
        // Un role personnalise a moitie migre : il detient la cle composite et UNE de ses trois
        // cles fines. Le rapport lui doit exactement les deux autres - pas une de plus.
        var roleName = $"rbac.custom.{Guid.NewGuid():N}";
        await CreateRoleAsync(roleName, PermissionCatalog.UsersWrite, PermissionCatalog.AdminUserCreate, PermissionCatalog.HrPayrollClose);

        using (var withoutKey = await LoginAsync($"rbac.report.none.{Guid.NewGuid():N}"[..40], UnrelatedKey))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await withoutKey.GetAsync("/api/v1/security/permission-migration-report")).StatusCode);
        }

        foreach (var key in new[] { PermissionCatalog.RolesRead, PermissionCatalog.AdminRoleRead })
        {
            using var reader = await LoginAsync($"rbac.report.{Guid.NewGuid():N}"[..40], key);

            var report = await reader.GetFromJsonAsync<PermissionMigrationReport>(
                "/api/v1/security/permission-migration-report",
                RaqmiApiFactory.JsonOptions);

            Assert.NotNull(report);
            Assert.Equal(PermissionRegistry.LegacyKeys.Count, report!.LegacyKeyCount);
            Assert.Equal(PermissionRegistry.All.Count, report.TargetKeyCount);

            // Aucun role systeme : le seeder les migre lui-meme.
            Assert.DoesNotContain(report.Roles, row => row.Name == RoleCatalog.SystemAdministrator);
            Assert.DoesNotContain(report.Roles, row => row.Name == RoleCatalog.Cashier);

            var row = Assert.Single(report.Roles, candidate => candidate.Name == roleName);
            Assert.True(row.IsActive);
            Assert.False(row.IsMigrated);
            Assert.Equal(new[] { PermissionCatalog.UsersWrite }, row.LegacyKeysHeld);
            Assert.Equal(new[] { PermissionCatalog.AdminUserCreate, PermissionCatalog.HrPayrollClose }, row.TargetKeysHeld);
            Assert.Equal(new[] { PermissionCatalog.AdminUserDeactivate, PermissionCatalog.AdminUserUpdate }, row.TargetKeysMissing);
        }
    }

    [Fact]
    public async Task The_token_still_carries_one_permission_claim_per_held_key_and_nothing_else()
    {
        // Structure du JWT inchangee : un claim "permission" par cle detenue, aucun claim
        // derive - les alias vivent dans les politiques, pas dans le jeton. Un profil qui
        // detient la cle historique et la cle cible voit les deux ; celui qui n'en detient
        // qu'une n'en voit qu'une.
        var userName = $"rbac.jwt.{Guid.NewGuid():N}"[..40];
        var heldKeys = new[] { PermissionCatalog.AccountingRead, PermissionCatalog.FinanceAccountingRead, PermissionCatalog.FinanceEntryPost };

        await CreateUserAsync(userName, heldKeys);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userName, Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(login);

        var expected = heldKeys.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, login!.User.Permissions.Order(StringComparer.Ordinal).ToArray());

        var token = new JsonWebToken(login.AccessToken);

        var permissionClaims = token.Claims
            .Where(claim => claim.Type == SecurityClaimTypes.Permission)
            .Select(claim => claim.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, permissionClaims);

        // Aucun autre type de claim ne transporte une cle de permission.
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Type != SecurityClaimTypes.Permission && heldKeys.Contains(claim.Value, StringComparer.Ordinal));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string body)
    {
        return string.IsNullOrEmpty(body)
            ? await client.PostAsync(route, content: null)
            : await client.PostAsync(route, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
    }

    private async Task<HttpClient> LoginAsync(string userName, params string[] permissionKeys)
    {
        var safeUserName = userName.Length > 40 ? userName[..40] : userName;

        await CreateUserAsync(safeUserName, permissionKeys);

        return await _factory.CreateAuthenticatedClientAsync(safeUserName, Password);
    }

    /// <summary>
    /// Un utilisateur porte par un role dedie qui ne detient QUE les cles demandees - toutes
    /// deja seedees depuis PermissionCatalog, l'assertion echoue clairement sinon.
    /// </summary>
    private async Task CreateUserAsync(string userName, IReadOnlyCollection<string> permissionKeys)
    {
        var role = await CreateRoleAsync($"test.rbac.{Guid.NewGuid():N}", permissionKeys.ToArray());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var trackedRole = await dbContext.Roles.SingleAsync(candidate => candidate.Name == role.Name);

        var user = new User(userName, $"{userName}@example.com", "RBAC matrix", passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(trackedRole, DateTimeOffset.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Role> CreateRoleAsync(string roleName, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Cles absentes du catalogue seede : " + string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(roleName, "RBAC matrix role", "Role dedie a la matrice de politiques RBAC.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        return role;
    }
}
