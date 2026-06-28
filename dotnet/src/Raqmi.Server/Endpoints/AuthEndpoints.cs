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
                var user = DemoUserStore.FindByEmail(DemoData.Tenant.Id, email);
                if (user is null || !user.Active || user.Password != password)
                {
                    return Results.Json(new { error = "Identifiants invalides" }, JsonDefaults.Options, statusCode: 401);
                }

                DemoAuditStore.Add("login", "administration", "User", user.Id, $"Connexion : {user.Email}", user.Id);
                var token = JwtService.CreateToken(config, user);
                var permissions = DemoRoleStore.Get(user.RoleCode)?.Permissions ?? [];

                return Results.Json(new
                {
                    token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        fullName = user.FullName,
                        roleCode = user.RoleCode,
                        tenant = TenantResolver.ToDto(DemoData.Tenant),
                        siteIds = user.SiteIds,
                        permissions,
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
            var permissions = runtime.DemoMode
                ? (IReadOnlyList<string>)(DemoRoleStore.Get(roleCode)?.Permissions ?? [])
                : (IReadOnlyList<string>)new[] { "*" };

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
                    permissions,
                    tenant,
                },
            }, JsonDefaults.Options);
        });
    }
}
