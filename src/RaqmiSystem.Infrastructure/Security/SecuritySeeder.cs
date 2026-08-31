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
            PermissionCatalog.SettingsRead,
            PermissionCatalog.AccountingRead,
            PermissionCatalog.BudgetRead,
            // budget.approve is direction's alone: approving a budget is what freezes the year's
            // targets for everyone else, so it stays with the role that answers for them.
            PermissionCatalog.BudgetApprove,
            PermissionCatalog.ReceivablesRead,
            PermissionCatalog.TariffsRead,
            PermissionCatalog.LodgingRead,
            // Direction reads the housekeeping board but never runs it: planning a sheet and
            // signing off a room are unit-level acts, and housekeeping.write / .inspect stay
            // with the roles that answer for the floor.
            PermissionCatalog.HousekeepingRead,
            // Direction reads the CRM - segments, loyalty, campaigns, NPS - and writes
            // none of it. Qualifying a guest and moving their points are unit-level acts,
            // so crm.write and crm.loyalty stay with the roles that answer for the guest.
            PermissionCatalog.CrmRead,
            PermissionCatalog.ApprovalsRead,
            PermissionCatalog.ApprovalsDecide,
            PermissionCatalog.ReportsRead,
            // maintenance.read lets direction verify backups actually run; maintenance.backup
            // appears in NO list below, so only system.administrator triggers one, through the
            // catch-all grant of PermissionCatalog.All on that role.
            PermissionCatalog.MaintenanceRead,
            // Direction reads the HR module - headcount, contracts, payroll totals - and writes
            // none of it. hr.write, hr.payroll and hr.payroll.close stay with the HR profile:
            // preparing and closing a payroll is an HR act, not a governance one.
            PermissionCatalog.HrRead,
            // Wave E1 - stocks, achats, cuisine. Direction reads the three, and holds the two
            // acts that engage the establishment rather than run it: validating a physical
            // count (it writes the adjustment movements that redress the book stock) and
            // approving a purchase order (the moment the spend is committed). Everything
            // operational - entries, issues, transfers, order entry, receptions, recipe
            // sheets, HACCP readings - stays with the roles that answer for the floor.
            PermissionCatalog.InventoryRead,
            PermissionCatalog.InventoryValidate,
            PermissionCatalog.PurchasingRead,
            PermissionCatalog.PurchasingApprove,
            PermissionCatalog.KitchenRead
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
            PermissionCatalog.SettingsRead,
            PermissionCatalog.AccountingRead,
            PermissionCatalog.AccountingWrite,
            PermissionCatalog.AccountingPost,
            PermissionCatalog.BudgetRead,
            PermissionCatalog.BudgetWrite,
            PermissionCatalog.ReceivablesRead,
            PermissionCatalog.ReceivablesWrite,
            PermissionCatalog.TariffsRead,
            PermissionCatalog.TariffsWrite,
            PermissionCatalog.LodgingRead,
            PermissionCatalog.LodgingWrite,
            PermissionCatalog.LodgingCheckin,
            PermissionCatalog.HousekeepingRead,
            PermissionCatalog.HousekeepingWrite,
            PermissionCatalog.HousekeepingInspect,
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite,
            PermissionCatalog.CrmLoyalty,
            PermissionCatalog.ApprovalsRead,
            PermissionCatalog.ApprovalsWrite,
            PermissionCatalog.ApprovalsDecide,
            PermissionCatalog.ReportsRead,
            // Wave E1 - the operating controller holds the whole chain end to end: stock
            // movements and physical counts (validation included), the supplier file, order
            // entry, approval and reception, and the kitchen's recipe sheets and HACCP log.
            PermissionCatalog.InventoryRead,
            PermissionCatalog.InventoryWrite,
            PermissionCatalog.InventoryValidate,
            PermissionCatalog.PurchasingRead,
            PermissionCatalog.PurchasingWrite,
            PermissionCatalog.PurchasingApprove,
            PermissionCatalog.PurchasingReceive,
            PermissionCatalog.KitchenRead,
            PermissionCatalog.KitchenWrite
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
            PermissionCatalog.SettingsRead,
            PermissionCatalog.BudgetRead,
            PermissionCatalog.BudgetWrite,
            PermissionCatalog.TariffsRead,
            PermissionCatalog.TariffsWrite,
            PermissionCatalog.LodgingRead,
            PermissionCatalog.LodgingWrite,
            PermissionCatalog.LodgingCheckin,
            PermissionCatalog.HousekeepingRead,
            PermissionCatalog.HousekeepingWrite,
            PermissionCatalog.HousekeepingInspect,
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite,
            PermissionCatalog.CrmLoyalty,
            PermissionCatalog.ApprovalsRead,
            PermissionCatalog.ApprovalsDecide,
            PermissionCatalog.ReportsRead,
            // Wave E1 - the unit manager runs the store, the ordering and the kitchen of the
            // house they answer for: movements, counts, supplier file, draft orders, goods
            // reception, recipe sheets and HACCP readings. NOT inventory.validate nor
            // purchasing.approve: closing a count against the book stock and committing the
            // spend are the two acts kept away from the person who entered the figures.
            PermissionCatalog.InventoryRead,
            PermissionCatalog.InventoryWrite,
            PermissionCatalog.PurchasingRead,
            PermissionCatalog.PurchasingWrite,
            PermissionCatalog.PurchasingReceive,
            PermissionCatalog.KitchenRead,
            PermissionCatalog.KitchenWrite
        ],
        [RoleCatalog.Cashier] =
        [
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.TreasuryWrite,
            PermissionCatalog.SettingsRead,
            PermissionCatalog.LodgingRead,
            PermissionCatalog.LodgingCheckin,
            // The front desk needs housekeeping.write to post minibar consumption onto a folio
            // at check-out - the same act as any other extra it already records. It does NOT
            // get housekeeping.inspect: signing a room off is the floor supervisor act.
            PermissionCatalog.HousekeepingRead,
            PermissionCatalog.HousekeepingWrite,
            // The front desk is where the relationship is actually recorded: the opt-in
            // collected at check-in, a room preference, the satisfaction card handed back,
            // the call taken this morning. It does NOT get crm.loyalty: crediting or
            // spending points moves something the guest can redeem, and that stays with
            // the roles that answer for the programme.
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite,
            PermissionCatalog.ApprovalsRead
        ],
        // The HR profile. It holds the whole HR module and nothing else of the ERP: the personal
        // data of law 18-07 and the payroll figures must not travel with an operating profile.
        // It gets approvals.read so absence and payroll requests routed through the workflow
        // module stay visible, but never approvals.decide - deciding a step is an operating act.
        [RoleCatalog.HrManager] =
        [
            PermissionCatalog.UnitsRead,
            PermissionCatalog.SettingsRead,
            PermissionCatalog.ApprovalsRead,
            PermissionCatalog.HrRead,
            PermissionCatalog.HrWrite,
            PermissionCatalog.HrPayroll,
            PermissionCatalog.HrPayrollClose
        ],
        [RoleCatalog.Reader] =
        [
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.SettingsRead,
            PermissionCatalog.AccountingRead,
            PermissionCatalog.BudgetRead,
            PermissionCatalog.ReceivablesRead,
            PermissionCatalog.TariffsRead,
            PermissionCatalog.LodgingRead,
            PermissionCatalog.HousekeepingRead,
            PermissionCatalog.CrmRead,
            PermissionCatalog.ApprovalsRead,
            // Wave E1 - read-only across the three new operating modules, like every other
            // module on this profile.
            PermissionCatalog.InventoryRead,
            PermissionCatalog.PurchasingRead,
            PermissionCatalog.KitchenRead
        ]
    };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedInitialAdminAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts the catalog permissions that are missing AND re-aligns the display fields (name,
    /// category, description) of the existing ones on the catalog: a wording fixed in
    /// PermissionCatalog must reach databases seeded before the fix, not only fresh installs.
    /// The KEY is never touched - it is the permission's identity, referenced by role grants and
    /// authorization policies. Idempotent: an already-aligned permission is left untouched.
    /// </summary>
    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingPermissions = await dbContext.Permissions
            .ToDictionaryAsync(
                permission => permission.Key,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        foreach (var definition in PermissionCatalog.All)
        {
            if (existingPermissions.TryGetValue(definition.Key, out var permission))
            {
                if (permission.SyncDefinition(definition.Name, definition.Category, definition.Description))
                {
                    permission.MarkUpdated("system", DateTimeOffset.UtcNow);
                }

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
            RoleCatalog.HrManager => new Role(roleName, "Responsable RH", "Collaborateurs, contrats, temps, absences et paie.", isSystem: true),
            _ => throw new InvalidOperationException("Unknown system role.")
        };
    }
}
