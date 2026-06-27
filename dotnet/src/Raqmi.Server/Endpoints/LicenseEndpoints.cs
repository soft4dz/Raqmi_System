using Raqmi.Licensing;

namespace Raqmi.Server.Endpoints;

public static class LicenseEndpoints
{
    public static void MapLicenseEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        app.MapGet("/api/v1/license/fingerprint", (LicenseStore store) =>
            Results.Json(new { fingerprint = store.GetFingerprint() }, JsonDefaults.Options));

        app.MapGet("/api/v1/license/status", async (HttpContext http, IConfiguration config, LicenseStore store) =>
        {
            var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
            if (principal is null)
            {
                return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
            }

            if (runtime.DemoMode)
            {
                var evaluation = LicensePolicy.Evaluate(DemoData.License, new LicenseEvaluationContext
                {
                    Now = DateTimeOffset.UtcNow,
                    UsersCount = 3,
                    SitesCount = 1,
                    StorageUsedGb = 12,
                });
                var pack = LicensePacks.All.First(p => p.Kind == DemoData.License.Kind);
                return Results.Json(new
                {
                    tenant = TenantResolver.ToDto(DemoData.Tenant),
                    license = DemoData.License,
                    evaluation,
                    pack = new { pack.Label, pack.Description },
                    mode = runtime.LicenseMode,
                    fingerprint = store.GetFingerprint(),
                }, JsonDefaults.Options);
            }

            var fileLicense = await store.LoadFromDiskAsync();
            if (fileLicense is null)
            {
                return Results.Json(new { error = "Aucune licence active" }, JsonDefaults.Options, statusCode: 404);
            }

            var eval = LicensePolicy.Evaluate(fileLicense, new LicenseEvaluationContext
            {
                Now = DateTimeOffset.UtcNow,
                UsersCount = 0,
                SitesCount = 0,
                StorageUsedGb = 0,
                LastOnlineCheckAt = store.GetLastOnlineCheckAt(),
            });
            var licensePack = LicensePacks.All.FirstOrDefault(p => p.Kind == fileLicense.Kind);
            return Results.Json(new
            {
                tenant = new { id = fileLicense.TenantId, code = fileLicense.TenantId, name = fileLicense.TenantName },
                license = fileLicense,
                evaluation = eval,
                pack = licensePack is null ? null : new { licensePack.Label, licensePack.Description },
                mode = runtime.LicenseMode,
                fingerprint = store.GetFingerprint(),
            }, JsonDefaults.Options);
        });

        app.MapPost("/api/v1/license/import", async (HttpContext http, IConfiguration config, LicenseStore store, ImportLicenseRequest body) =>
        {
            var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
            if (principal is null)
            {
                return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
            }

            if (principal.FindFirst("roleCode")?.Value != "admin")
            {
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);
            }

            if (string.IsNullOrWhiteSpace(body.Content))
            {
                return Results.Json(new { error = "Contenu licence requis" }, JsonDefaults.Options, statusCode: 400);
            }

            try
            {
                var payload = await store.ImportAsync(body.Content);
                var evaluation = LicensePolicy.Evaluate(payload, new LicenseEvaluationContext
                {
                    Now = DateTimeOffset.UtcNow,
                    UsersCount = runtime.DemoMode ? 3 : 0,
                    SitesCount = runtime.DemoMode ? 1 : 0,
                    StorageUsedGb = runtime.DemoMode ? 12 : 0,
                    LastOnlineCheckAt = store.GetLastOnlineCheckAt(),
                });
                return Results.Json(new
                {
                    message = "Licence importée",
                    license = payload,
                    evaluation,
                    fingerprint = store.GetFingerprint(),
                }, JsonDefaults.Options);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 400);
            }
        });
    }
}
