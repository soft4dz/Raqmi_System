using Raqmi.Shared;

namespace Raqmi.Licensing;

public sealed record LicensePackDefinition(
    LicenseKind Kind,
    string Label,
    string Description,
    LicenseLimits DefaultLimits,
    List<string> Modules);

public static class LicensePacks
{
    public static IReadOnlyList<LicensePackDefinition> All { get; } =
    [
        new(
            LicenseKind.Starter,
            "Starter",
            "Pack pour petite structure avec modules essentiels.",
            new LicenseLimits(10, 1, 10, 15),
            ["sites", "daily_revenue", "billing", "reports", "dashboards"]),
        new(
            LicenseKind.Professional,
            "Professional",
            "Pack hôtel ou entreprise moyenne avec finance, RH et GED.",
            new LicenseLimits(50, 5, 100, 30),
            [
                "sites", "daily_revenue", "treasury", "billing", "receivables", "contracts",
                "hr", "stocks", "purchases", "ged", "reports", "dashboards",
            ]),
        new(
            LicenseKind.Enterprise,
            "Enterprise",
            "Pack multi-sites complet avec exploitation avancée, cloud et synchronisation.",
            new LicenseLimits(250, 25, 1000, 45),
            [
                "sites", "daily_revenue", "treasury", "billing", "receivables", "contracts",
                "hr", "payroll", "stocks", "purchases", "maintenance", "ged", "parking",
                "beach_pool", "portmaster", "quality", "checklists", "reports", "dashboards",
                "sync", "cloud_storage",
            ]),
    ];
}
