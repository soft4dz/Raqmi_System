using Raqmi.Shared;

namespace Raqmi.Licensing;

public static class ModuleEntitlement
{
    public static bool IsEnabled(RaqmiModuleDefinition module, IEnumerable<string> allowedModules)
    {
        if (!module.Commercial)
        {
            return true;
        }

        var wire = RaqmiModuleCodes.ToWire(module.Code);
        return allowedModules.Any(code => string.Equals(code, wire, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> NormalizeAllowedModules(IEnumerable<string> modules)
    {
        return modules
            .Select(m => m.Trim().ToLowerInvariant())
            .Where(m => !string.IsNullOrEmpty(m))
            .Where(m => RaqmiModuleCodes.TryParse(m, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static RaqmiLicensePayload NormalizePayload(RaqmiLicensePayload payload) =>
        payload with { AllowedModules = NormalizeAllowedModules(payload.AllowedModules) };

    public static int CountEnabledModules(IEnumerable<string> allowedModules) =>
        ModuleCatalog.All.Count(m => IsEnabled(m, allowedModules));
}
