namespace Raqmi.Server;

public static class PermissionCatalog
{
    public static IReadOnlyList<object> List() =>
    [
        new { key = "administration:read", label = "Administration — lecture", module = "administration" },
        new { key = "administration:write", label = "Administration — écriture", module = "administration" },
        new { key = "sites:read", label = "Sites — lecture", module = "sites" },
        new { key = "sites:write", label = "Sites — écriture", module = "sites" },
        new { key = "settings:read", label = "Paramètres — lecture", module = "settings" },
        new { key = "settings:write", label = "Paramètres — écriture", module = "settings" },
        new { key = "finance:read", label = "Finance — lecture", module = "finance" },
        new { key = "finance:write", label = "Finance — écriture", module = "finance" },
        new { key = "hr:read", label = "RH — lecture", module = "hr" },
        new { key = "hr:write", label = "RH — écriture", module = "hr" },
        new { key = "stocks:read", label = "Stocks — lecture", module = "stocks" },
        new { key = "stocks:write", label = "Stocks — écriture", module = "stocks" },
        new { key = "ged:read", label = "GED — lecture", module = "ged" },
        new { key = "ged:write", label = "GED — écriture", module = "ged" },
    ];
}
