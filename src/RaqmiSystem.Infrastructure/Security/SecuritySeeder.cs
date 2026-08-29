using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Security;

public sealed class SecuritySeeder(
    RaqmiDbContext dbContext,
    IPasswordHasher passwordHasher) : ISecuritySeeder
{
    /// <summary>
    /// Note on the global settings (module "Parametrage global"): settings.read is granted to
    /// every role - the establishment's identity and the exploitation defaults are what all the
    /// screens display and pre-fill from. settings.write appears in NO list below: only
    /// system.administrator holds it, through the catch-all grant of PermissionCatalog.All on
    /// that role, because a change there is engaging for the whole installation (it is the
    /// identity frozen onto every invoice issued afterwards).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>
    {
        [RoleCatalog.SystemAdministrator] = PermissionCatalog.All.Select(permission => permission.Key).ToArray(),
        [RoleCatalog.Direction] =
        [
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
            PermissionCatalog.SettingsRead
        ],
        [RoleCatalog.ExploitationControl] =
        [
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.RevenueValidate,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.AuditRead,
            PermissionCatalog.ReportsExport,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.ClosingClose,
            PermissionCatalog.ClosingReopen,
            // treasury.read accompanies treasury.approve: a controller cannot meaningfully
            // approve payment orders without being able to consult treasury data first.
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.TreasuryApprove,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.CustomersWrite,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesWrite,
            PermissionCatalog.InvoicesIssue,
            PermissionCatalog.SettingsRead
        ],
        [RoleCatalog.UnitManager] =
        [
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.ReportsExport,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.ClosingClose,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.CustomersWrite,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesWrite,
            PermissionCatalog.SettingsRead
        ],
        [RoleCatalog.Cashier] =
        [
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.TreasuryWrite,
            PermissionCatalog.SettingsRead
        ],
        [RoleCatalog.Reader] =
        [
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.SettingsRead
        ]
    };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedInitialAdminAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingPermissions = await dbContext.Permissions
            .Select(permission => permission.Key)
            .ToArrayAsync(cancellationToken);

        var existing = existingPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in PermissionCatalog.All)
        {
            if (existing.Contains(definition.Key))
            {
                continue;
            }

            dbContext.Permissions.Add(new Permission(
                definition.Key,
                definition.Name,
                definition.Category,
                definition.Description));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RolePermissions.Keys)
        {
            var role = await dbContext.Roles
                .Include(currentRole => currentRole.Permissions)
                .SingleOrDefaultAsync(currentRole => currentRole.Name == roleName, cancellationToken);

            if (role is null)
            {
                role = CreateRole(roleName);
                dbContext.Roles.Add(role);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var permissionMap = await dbContext.Permissions
                .Where(permission => RolePermissions[roleName].Contains(permission.Key))
                .ToDictionaryAsync(permission => permission.Key, cancellationToken);

            foreach (var permissionKey in RolePermissions[roleName])
            {
                if (permissionMap.TryGetValue(permissionKey, out var permission))
                {
                    role.GrantPermission(permission, DateTimeOffset.UtcNow);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInitialAdminAsync(CancellationToken cancellationToken)
    {
        var email = Environment.GetEnvironmentVariable("RAQMI_INITIAL_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("RAQMI_INITIAL_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (password.Length < 12)
        {
            throw new InvalidOperationException("Initial administrator password must be at least 12 characters long.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var user = await dbContext.Users
            .Include(currentUser => currentUser.Roles)
            .SingleOrDefaultAsync(currentUser => currentUser.NormalizedEmail == normalizedEmail, cancellationToken);

        var adminRole = await dbContext.Roles
            .SingleAsync(role => role.Name == RoleCatalog.SystemAdministrator, cancellationToken);

        if (user is null)
        {
            user = new User(
                email.Split('@')[0],
                email,
                "Administrateur Raqmi",
                passwordHasher.Hash(password),
                mustChangePassword: true);

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        user.AssignRole(adminRole, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Role CreateRole(string roleName)
    {
        return roleName switch
        {
            RoleCatalog.SystemAdministrator => new Role(roleName, "Administrateur systeme", "Acces complet au socle Raqmi System.", isSystem: true),
            RoleCatalog.Direction => new Role(roleName, "Direction", "Consultation direction, tableaux de bord et reporting.", isSystem: true),
            RoleCatalog.ExploitationControl => new Role(roleName, "Exploitation et controle", "Controle des recettes, validation et audit.", isSystem: true),
            RoleCatalog.UnitManager => new Role(roleName, "Responsable unite", "Gestion operationnelle d'une unite hoteliere.", isSystem: true),
            RoleCatalog.Cashier => new Role(roleName, "Caissier", "Saisie caisse, recettes et mouvements de tresorerie.", isSystem: true),
            RoleCatalog.Reader => new Role(roleName, "Lecture seule", "Consultation limitee des donnees autorisees.", isSystem: true),
            _ => throw new InvalidOperationException("Unknown system role.")
        };
    }
}
