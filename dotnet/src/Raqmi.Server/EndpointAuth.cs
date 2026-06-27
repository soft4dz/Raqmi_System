namespace Raqmi.Server;

public static class EndpointAuth
{
    public static string GetTenantId(HttpContext http)
    {
        var tenantId = http.User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Tenant introuvable dans le token");
        }

        return tenantId;
    }

    public static string? GetUserId(HttpContext http) =>
        http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
}
