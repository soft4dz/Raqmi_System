using System.Security.Claims;
using Raqmi.Data;

namespace Raqmi.Server;

public sealed class HttpTenantContext(IHttpContextAccessor http) : ITenantContext
{
    public string? TenantId => http.HttpContext?.User?.FindFirst("tenantId")?.Value;

    public string? UserId => http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? http.HttpContext?.User?.FindFirst("sub")?.Value;
}
