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

    public static string GetRoleCode(HttpContext http) =>
        http.User.FindFirst("roleCode")?.Value ?? "user";

    public static bool IsAdmin(HttpContext http) =>
        string.Equals(GetRoleCode(http), "admin", StringComparison.Ordinal);

    public static bool HasPermission(HttpContext http, ServerRuntime runtime, string module, string action)
    {
        if (IsAdmin(http)) return true;
        if (!runtime.DemoMode) return false;
        return DemoRoleStore.HasPermission(GetRoleCode(http), module, action);
    }

    public static bool CanReadAdmin(HttpContext http, ServerRuntime runtime) =>
        IsAdmin(http) || HasPermission(http, runtime, "administration", "read");

    public static bool CanWriteAdmin(HttpContext http, ServerRuntime runtime) =>
        IsAdmin(http) || HasPermission(http, runtime, "administration", "write");

    public static bool CanWriteSites(HttpContext http, ServerRuntime runtime) =>
        IsAdmin(http) || HasPermission(http, runtime, "sites", "write");

    public static bool CanWriteSettings(HttpContext http, ServerRuntime runtime) =>
        IsAdmin(http) || HasPermission(http, runtime, "settings", "write");

    public static IReadOnlyList<string> PermissionsFor(HttpContext http, ServerRuntime runtime)
    {
        if (IsAdmin(http)) return ["*"];
        if (!runtime.DemoMode) return ["*"];
        return DemoRoleStore.Get(GetRoleCode(http))?.Permissions ?? [];
    }
}
