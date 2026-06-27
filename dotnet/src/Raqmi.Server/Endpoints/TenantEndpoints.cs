namespace Raqmi.Server.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        app.MapGet("/api/v1/tenant/current", (HttpContext http, IConfiguration config) =>
        {
            var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
            if (principal is null)
            {
                return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
            }

            var tenant = TenantResolver.Resolve(principal, runtime);
            if (tenant is null)
            {
                return Results.Json(new { error = "Tenant introuvable" }, JsonDefaults.Options, statusCode: 404);
            }

            return Results.Json(new { tenant }, JsonDefaults.Options);
        });
    }
}
