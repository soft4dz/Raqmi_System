using Raqmi.Licensing;
using Raqmi.Shared;

namespace Raqmi.Server.Endpoints;

public static class ModuleEndpoints
{
    public static void MapModuleEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        app.MapGet("/api/v1/modules", async (HttpContext http, IConfiguration config, LicenseStore store) =>
        {
            var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
            if (principal is null)
            {
                return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
            }

            var allowedModules = runtime.DemoMode
                ? DemoData.License.AllowedModules
                : (await store.LoadFromDiskAsync())?.AllowedModules ?? [];

            var modules = ModuleCatalog.All.Select(m => ModuleMapper.ToDto(m, allowedModules));
            return Results.Json(new { modules }, JsonDefaults.Options);
        });

        app.MapGet("/api/v1/modules/catalog", () =>
            Results.Json(new { modules = ModuleCatalog.All.Select(m => ModuleMapper.ToDto(m, true)) }, JsonDefaults.Options));
    }
}
