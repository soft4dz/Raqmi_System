namespace Raqmi.Shared;

public enum RaqmiModuleCode
{
    Administration,
    Settings,
    Sites,
    DailyRevenue,
    Treasury,
    Billing,
    Receivables,
    Contracts,
    Hr,
    Payroll,
    Stocks,
    Purchases,
    Maintenance,
    Ged,
    Parking,
    BeachPool,
    Portmaster,
    Quality,
    Checklists,
    Reports,
    Dashboards,
    Sync,
    CloudStorage,
}

public static class RaqmiModuleCodes
{
    public static string ToWire(RaqmiModuleCode code) => code switch
    {
        RaqmiModuleCode.Administration => "administration",
        RaqmiModuleCode.Settings => "settings",
        RaqmiModuleCode.Sites => "sites",
        RaqmiModuleCode.DailyRevenue => "daily_revenue",
        RaqmiModuleCode.Treasury => "treasury",
        RaqmiModuleCode.Billing => "billing",
        RaqmiModuleCode.Receivables => "receivables",
        RaqmiModuleCode.Contracts => "contracts",
        RaqmiModuleCode.Hr => "hr",
        RaqmiModuleCode.Payroll => "payroll",
        RaqmiModuleCode.Stocks => "stocks",
        RaqmiModuleCode.Purchases => "purchases",
        RaqmiModuleCode.Maintenance => "maintenance",
        RaqmiModuleCode.Ged => "ged",
        RaqmiModuleCode.Parking => "parking",
        RaqmiModuleCode.BeachPool => "beach_pool",
        RaqmiModuleCode.Portmaster => "portmaster",
        RaqmiModuleCode.Quality => "quality",
        RaqmiModuleCode.Checklists => "checklists",
        RaqmiModuleCode.Reports => "reports",
        RaqmiModuleCode.Dashboards => "dashboards",
        RaqmiModuleCode.Sync => "sync",
        RaqmiModuleCode.CloudStorage => "cloud_storage",
        _ => code.ToString().ToLowerInvariant(),
    };

    public static bool TryParse(string wire, out RaqmiModuleCode code)
    {
        foreach (var value in Enum.GetValues<RaqmiModuleCode>())
        {
            if (ToWire(value) == wire)
            {
                code = value;
                return true;
            }
        }

        code = default;
        return false;
    }
}
