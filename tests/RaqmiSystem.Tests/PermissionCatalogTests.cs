using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Permission_keys_are_unique()
    {
        var keys = PermissionCatalog.All.Select(permission => permission.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Permission_keys_are_normalized()
    {
        Assert.All(PermissionCatalog.All, permission =>
        {
            Assert.Equal(permission.Key, permission.Key.ToLowerInvariant());
            Assert.DoesNotContain(" ", permission.Key);
        });
    }

    /// <summary>
    /// Les 83 cles historiques restent dans le catalogue, avec leur valeur : le client WPF, le
    /// garde de readiness et les roles des installations en service les referencent. Le registre
    /// s'y AJOUTE ; rien ne s'en retire avant une version de compatibilite (docs/security.md).
    /// </summary>
    [Fact]
    public void The_83_historical_keys_are_all_still_in_the_catalog()
    {
        var historicalKeys = new[]
        {
            "users.read", "users.write", "roles.read", "roles.write", "units.read", "units.write",
            "revenue.read", "revenue.write", "revenue.validate", "dashboard.read",
            "treasury.read", "treasury.write", "audit.read", "reports.export", "security.seed",
            "closing.read", "closing.close", "closing.reopen", "treasury.approve",
            "customers.read", "customers.write", "invoices.read", "invoices.write", "invoices.issue",
            "settings.read", "settings.write",
            "accounting.read", "accounting.write", "accounting.post", "accounting.reconcile",
            "accounting.close", "accounting.reverse", "accounting.admin",
            "budget.read", "budget.write", "budget.approve", "receivables.read", "receivables.write",
            "tariffs.read", "tariffs.write",
            "lodging.read", "lodging.write", "lodging.checkin", "lodging.reserve", "lodging.checkout",
            "lodging.change_rate", "lodging.room_move", "lodging.override_restriction", "lodging.overbooking",
            "lodging.noshow", "lodging.cancel", "lodging.manage_rooms", "lodging.manage_rates", "lodging.night_audit",
            "housekeeping.read", "housekeeping.write", "housekeeping.inspect",
            "crm.read", "crm.write", "crm.loyalty",
            "approvals.read", "approvals.write", "approvals.decide",
            "reports.read", "maintenance.read", "maintenance.backup", "sync.read",
            "mice.read", "mice.write",
            "hr.read", "hr.write", "hr.payroll", "hr.payroll.close",
            "inventory.read", "inventory.write", "inventory.validate",
            "purchasing.read", "purchasing.write", "purchasing.approve", "purchasing.receive",
            "kitchen.read", "kitchen.write", "kpi.admin"
        };

        Assert.Equal(83, historicalKeys.Length);
        Assert.Equal(historicalKeys, PermissionCatalog.Legacy.Select(definition => definition.Key).ToArray());

        var catalogKeys = PermissionCatalog.All.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);
        Assert.All(historicalKeys, key => Assert.Contains(key, catalogKeys));
    }

    [Fact]
    public void The_catalog_is_the_historical_keys_followed_by_the_registry_keys_once_each()
    {
        var registryKeys = PermissionRegistry.All.Select(target => target.Key).ToHashSet(StringComparer.Ordinal);
        var catalogKeys = PermissionCatalog.All.Select(definition => definition.Key).ToArray();

        // Chaque cle cible est seedee et dotee d'une politique...
        Assert.All(registryKeys, key => Assert.Contains(key, catalogKeys));

        // ...une seule fois, meme quand elle etait deja historique (hr.payroll.close).
        Assert.Equal(catalogKeys.Length, catalogKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(PermissionCatalog.Legacy.Count + registryKeys.Count - 1, catalogKeys.Length);

        // Les cles cibles portent leur prefixe de domaine comme categorie d'affichage.
        foreach (var target in PermissionRegistry.All.Where(target => target.Key != PermissionCatalog.HrPayrollClose))
        {
            var definition = PermissionCatalog.All.Single(candidate => candidate.Key == target.Key);
            Assert.Equal(target.Prefix, definition.Category);
            Assert.Equal(target.Name, definition.Name);
            Assert.Equal(target.Description, definition.Description);
        }
    }
}
