using System.Windows.Controls;
using Raqmi.Client.Views.Admin;
using Raqmi.Client.Views.Core;
using Raqmi.Client.Views.Finance;
using Raqmi.Client.Views.Ged;
using Raqmi.Client.Views.Hr;
using Raqmi.Client.Views.Stocks;

namespace Raqmi.Client.Navigation;

public sealed class ModuleNavigator
{
    private readonly BusinessApiClient _business;

    public ModuleNavigator(RaqmiApiClient api) => _business = new BusinessApiClient(api);

    public BusinessApiClient Business => _business;

    public void SetActiveSiteId(string? siteId) => _business.SetActiveSiteId(siteId);

    public static string? DefaultScreenForModule(string moduleCode) => moduleCode switch
    {
        "administration" => "admin_users",
        "settings" => "core_settings",
        "sites" => "core_sites",
        "daily_revenue" => "daily_revenue",
        "billing" => "billing",
        "treasury" => "treasury",
        "hr" => "hr_employees",
        "stocks" => "stocks_products",
        "ged" => "ged",
        _ => null,
    };

    public static IReadOnlyList<NavItem> BuildNavItems(IEnumerable<ModuleDto> modules)
    {
        var enabled = modules.Where(m => m.Enabled).Select(m => m.Code).ToHashSet(StringComparer.Ordinal);
        var items = new List<NavItem> { new("dashboard", "Dashboard", "core") };

        if (enabled.Contains("sites")) items.Add(new("core_sites", "Sites / unités", "core"));
        if (enabled.Contains("settings")) items.Add(new("core_settings", "Paramétrage global", "core"));
        if (enabled.Contains("administration"))
        {
            items.Add(new("admin_users", "Utilisateurs", "core"));
            items.Add(new("admin_roles", "Rôles & permissions", "core"));
            items.Add(new("admin_audit", "Journal d'audit", "core"));
        }
        if (enabled.Contains("daily_revenue")) items.Add(new("daily_revenue", "Recettes journalières", "finance"));
        if (enabled.Contains("billing")) items.Add(new("billing", "Facturation", "finance"));
        if (enabled.Contains("treasury")) items.Add(new("treasury", "Trésorerie", "finance"));
        if (enabled.Contains("hr"))
        {
            items.Add(new("hr_employees", "Employés", "hr"));
            items.Add(new("hr_contracts", "Contrats", "hr"));
            items.Add(new("hr_attendance", "Pointage", "hr"));
        }
        if (enabled.Contains("stocks"))
        {
            items.Add(new("stocks_products", "Produits", "operations"));
            items.Add(new("stocks_movements", "Mouvements", "operations"));
            items.Add(new("stocks_inventory", "Inventaire", "operations"));
        }
        if (enabled.Contains("ged")) items.Add(new("ged", "Documents", "system"));

        return items;
    }

    public UserControl CreateView(string screenKey) => screenKey switch
    {
        "core_sites" => new SiteListView(_business),
        "core_settings" => new TenantSettingsView(_business),
        "admin_users" => new UserListView(_business),
        "admin_roles" => new RoleListView(_business),
        "admin_audit" => new AuditLogView(_business),
        "daily_revenue" => new DailyRevenueView(_business),
        "billing" => new BillingView(_business),
        "treasury" => new TreasuryView(_business),
        "hr_employees" => new EmployeeListView(_business),
        "hr_contracts" => new ContractView(_business),
        "hr_attendance" => new AttendanceView(_business),
        "stocks_products" => new ProductListView(_business),
        "stocks_movements" => new StockMovementView(_business),
        "stocks_inventory" => new InventoryView(_business),
        "ged" => new DocumentLibraryView(_business),
        _ => throw new ArgumentException($"Écran inconnu: {screenKey}", nameof(screenKey)),
    };
}

public sealed record NavItem(string Key, string Label, string Family);
