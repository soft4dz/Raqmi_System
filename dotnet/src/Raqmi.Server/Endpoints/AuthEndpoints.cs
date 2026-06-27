namespace Raqmi.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        app.MapPost("/api/v1/auth/login", (LoginRequest request, IConfiguration config) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            var password = request.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Json(new { error = "Email et mot de passe requis" }, JsonDefaults.Options, statusCode: 400);
            }

            if (runtime.DemoMode)
            {
                if (email != DemoData.User.Email || password != DemoData.User.Password)
                {
                    return Results.Json(new { error = "Identifiants invalides" }, JsonDefaults.Options, statusCode: 401);
                }

                var token = JwtService.CreateToken(config, DemoData.User);
                return Results.Json(new
                {
                    token,
                    user = new
                    {
                        id = DemoData.User.Id,
                        email = DemoData.User.Email,
                        fullName = DemoData.User.FullName,
                        roleCode = DemoData.User.RoleCode,
                        tenant = TenantResolver.ToDto(DemoData.Tenant),
                    },
                }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        });

        app.MapGet("/api/v1/auth/me", (HttpContext http, IConfiguration config) =>
        {
            var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
            if (principal is null)
            {
                return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
            }

            var tenant = TenantResolver.Resolve(principal, runtime);
            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value ?? string.Empty;
            var roleCode = principal.FindFirst("roleCode")?.Value ?? "user";
            var siteIds = runtime.DemoMode
                ? DemoUserStore.GetSiteIds(DemoData.Tenant.Id, userId)
                : Array.Empty<string>();

            return Results.Json(new
            {
                user = new
                {
                    id = userId,
                    email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                    fullName = principal.FindFirst("fullName")?.Value,
                    roleCode,
                    tenantId = principal.FindFirst("tenantId")?.Value,
                    siteIds,
                    tenant,
                },
            }, JsonDefaults.Options);
        });
    }
}
