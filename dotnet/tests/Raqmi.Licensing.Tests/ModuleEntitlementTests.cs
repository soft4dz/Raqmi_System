using Raqmi.Licensing;
using Raqmi.Shared;

namespace Raqmi.Licensing.Tests;

public class ModuleEntitlementTests
{
    [Fact]
    public void ProfessionalPack_EnablesExactly14Modules()
    {
        var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
        var enabled = ModuleEntitlement.CountEnabledModules(pack.Modules);

        Assert.Equal(14, enabled);
        Assert.Equal(23, ModuleCatalog.All.Count);
    }

    [Fact]
    public void ProfessionalPack_EnablesCoreModulesWithoutLicenseEntry()
    {
        var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
        var admin = ModuleCatalog.All.First(m => m.Code == RaqmiModuleCode.Administration);
        var settings = ModuleCatalog.All.First(m => m.Code == RaqmiModuleCode.Settings);

        Assert.True(ModuleEntitlement.IsEnabled(admin, pack.Modules));
        Assert.True(ModuleEntitlement.IsEnabled(settings, pack.Modules));
    }

    [Fact]
    public void ProfessionalPack_BlocksEnterpriseOnlyModules()
    {
        var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
        var payroll = ModuleCatalog.All.First(m => m.Code == RaqmiModuleCode.Payroll);
        var sync = ModuleCatalog.All.First(m => m.Code == RaqmiModuleCode.Sync);

        Assert.False(ModuleEntitlement.IsEnabled(payroll, pack.Modules));
        Assert.False(ModuleEntitlement.IsEnabled(sync, pack.Modules));
    }

    [Fact]
    public void NormalizeAllowedModules_IsCaseInsensitiveAndFiltersUnknown()
    {
        var normalized = ModuleEntitlement.NormalizeAllowedModules([
            "Sites",
            " DAILY_REVENUE ",
            "unknown_module",
            "billing",
        ]);

        Assert.Equal(["sites", "daily_revenue", "billing"], normalized);
    }

    [Fact]
    public void Enterprise_IncludesAllProfessionalModules()
    {
        var professional = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
        var enterprise = LicensePacks.All.First(p => p.Kind == LicenseKind.Enterprise);

        foreach (var module in professional.Modules)
        {
            Assert.Contains(module, enterprise.Modules);
        }
    }
}
