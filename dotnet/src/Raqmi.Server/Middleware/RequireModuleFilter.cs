using Raqmi.Licensing;
using Raqmi.Server;
using Raqmi.Shared;

namespace Raqmi.Server.Middleware;

public sealed class RequireModuleFilter(string moduleCode, LicenseStore licenseStore, ServerRuntime runtime) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        var principal = JwtService.ValidateToken(config, http.Request.Headers.Authorization);
        if (principal is null)
        {
            return Results.Json(new { error = "Non authentifié" }, JsonDefaults.Options, statusCode: 401);
        }

        http.User = principal;

        IReadOnlyList<string> allowed;
        if (runtime.DemoMode)
        {
            allowed = DemoData.License.AllowedModules;
        }
        else
        {
            var payload = await licenseStore.LoadFromDiskAsync();
            if (payload is null)
            {
                return Results.Json(new { error = "Aucune licence active" }, JsonDefaults.Options, statusCode: 403);
            }
            allowed = payload.AllowedModules;
        }

        var module = ModuleCatalog.All.FirstOrDefault(m =>
            string.Equals(RaqmiModuleCodes.ToWire(m.Code), moduleCode, StringComparison.OrdinalIgnoreCase));
        if (module is null)
        {
            return Results.Json(new { error = $"Module inconnu: {moduleCode}" }, JsonDefaults.Options, statusCode: 403);
        }

        if (!ModuleEntitlement.IsEnabled(module, allowed))
        {
            return Results.Json(new { error = $"Module non autorisé: {moduleCode}" }, JsonDefaults.Options, statusCode: 403);
        }

        return await next(context);
    }
}

public static class EndpointExtensions
{
    public static RouteHandlerBuilder RequireRaqmiModule(this RouteHandlerBuilder builder, string moduleCode) =>
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var licenseStore = factoryContext.ApplicationServices.GetRequiredService<LicenseStore>();
            var runtime = factoryContext.ApplicationServices.GetRequiredService<ServerRuntime>();
            var filter = new RequireModuleFilter(moduleCode, licenseStore, runtime);
            return invocationContext => filter.InvokeAsync(invocationContext, next);
        });
}
