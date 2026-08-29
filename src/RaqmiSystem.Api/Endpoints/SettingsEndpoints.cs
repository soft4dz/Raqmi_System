using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var settings = api.MapGroup("/settings")
            .WithTags("Settings");

        // Never 404s: an installation always runs with settings, whether or not an administrator
        // has written them yet (see ApplicationSettingsService.GetAsync).
        settings.MapGet("", async (
            IApplicationSettingsService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.SettingsRead);

        settings.MapPut("", async (
            UpdateApplicationSettingsRequest request,
            IApplicationSettingsService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.SettingsWrite);

        return api;
    }
}
