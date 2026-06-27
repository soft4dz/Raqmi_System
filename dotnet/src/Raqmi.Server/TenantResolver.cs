using System.Security.Claims;

namespace Raqmi.Server;

public static class TenantResolver
{
    public static object? Resolve(ClaimsPrincipal principal, ServerRuntime runtime, LicenseStore? store = null)
    {
        var tenantId = principal.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (runtime.DemoMode && tenantId == DemoData.Tenant.Id)
        {
            return ToDto(DemoData.Tenant);
        }

        return new { id = tenantId, code = tenantId, name = tenantId };
    }

    public static object ToDto(DemoTenant tenant) => new
    {
        id = tenant.Id,
        code = tenant.Code,
        name = tenant.Name,
    };
}
