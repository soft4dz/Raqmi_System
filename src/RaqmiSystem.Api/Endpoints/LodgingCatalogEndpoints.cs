using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10 - les referentiels commerciaux du PMS : extras, forfaits, politiques d'annulation et
/// regles de revenue management.
///
/// PERMISSIONS : "lodging.read" en lecture, "lodging.manage_rates" en ecriture. Ces quatre
/// referentiels changent ce que l'hotel vend et a quel prix ; ils ne sont pas du ressort du
/// comptoir.
/// </summary>
internal static class LodgingCatalogEndpoints
{
    public static RouteGroupBuilder MapLodgingCatalogEndpoints(this RouteGroupBuilder api)
    {
        MapExtraEndpoints(api);
        MapPackageEndpoints(api);
        MapCancellationPolicyEndpoints(api);
        MapYieldRuleEndpoints(api);
        return api;
    }

    private static void MapExtraEndpoints(RouteGroupBuilder api)
    {
        var extras = api.MapGroup("/lodging/extras").WithTags("LodgingCatalog");

        extras.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingCatalogService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListExtrasAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        extras.MapPost("", async (
            SaveExtraItemRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateExtraAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/extras/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        extras.MapPut("/{id:guid}", async (
            Guid id,
            SaveExtraItemRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateExtraAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        extras.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetExtraActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        extras.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetExtraActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);
    }

    private static void MapPackageEndpoints(RouteGroupBuilder api)
    {
        var packages = api.MapGroup("/lodging/packages").WithTags("LodgingCatalog");

        packages.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingCatalogService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListPackagesAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        packages.MapPost("", async (
            SavePackageRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePackageAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/packages/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        packages.MapPut("/{id:guid}", async (
            Guid id,
            SavePackageRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePackageAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        packages.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPackageActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        packages.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPackageActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);
    }

    private static void MapCancellationPolicyEndpoints(RouteGroupBuilder api)
    {
        var policies = api.MapGroup("/lodging/cancellation-policies").WithTags("LodgingCatalog");

        policies.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingCatalogService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListCancellationPoliciesAsync(
                hotelUnitCode,
                includeInactive == true,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        policies.MapPost("", async (
            SaveCancellationPolicyRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCancellationPolicyAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/cancellation-policies/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        policies.MapPut("/{id:guid}", async (
            Guid id,
            SaveCancellationPolicyRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCancellationPolicyAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        policies.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCancellationPolicyActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        policies.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCancellationPolicyActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);
    }

    private static void MapYieldRuleEndpoints(RouteGroupBuilder api)
    {
        var rules = api.MapGroup("/lodging/yield-rules").WithTags("LodgingCatalog");

        rules.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingCatalogService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListYieldRulesAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        rules.MapPost("", async (
            SaveYieldRuleRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateYieldRuleAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/yield-rules/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        rules.MapPut("/{id:guid}", async (
            Guid id,
            SaveYieldRuleRequest request,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateYieldRuleAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        rules.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetYieldRuleActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);

        rules.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingCatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetYieldRuleActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRateManage);
    }
}
