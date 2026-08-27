using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder api)
    {
        var units = api.MapGroup("/organization/hotel-units")
            .WithTags("Hotel units");

        units.MapGet("", async (
            bool includeInactive,
            IHotelUnitService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(includeInactive, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.UnitsRead);

        units.MapGet("/{code}", async (
            string code,
            IHotelUnitService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UnitsRead);

        units.MapPost("", async (
            CreateHotelUnitRequest request,
            IHotelUnitService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/organization/hotel-units/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UnitsWrite);

        units.MapPut("/{code}", async (
            string code,
            UpdateHotelUnitRequest request,
            IHotelUnitService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UnitsWrite);

        units.MapPost("/{code}/activate", async (
            string code,
            IHotelUnitService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UnitsWrite);

        units.MapPost("/{code}/deactivate", async (
            string code,
            IHotelUnitService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UnitsWrite);

        return api;
    }
}
