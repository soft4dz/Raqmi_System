using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Tariffs and conventions module (rate plans, rate periods, customer conventions, nightly-rate
/// resolution). Policy names are the permission keys registered in Program.cs from
/// PermissionCatalog ("tariffs.read"/"tariffs.write").
/// </summary>
internal static class TariffsEndpoints
{
    private const string TariffsRead = PermissionCatalog.TariffsRead;

    private const string TariffsWrite = PermissionCatalog.TariffsWrite;

    public static RouteGroupBuilder MapTariffsEndpoints(this RouteGroupBuilder api)
    {
        MapPlanEndpoints(api);
        MapPeriodEndpoints(api);
        MapConventionEndpoints(api);
        MapResolveEndpoint(api);
        return api;
    }

    private static void MapPlanEndpoints(RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/tariffs/plans")
            .WithTags("Tariffs");

        plans.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ITariffService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPlansAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(TariffsRead);

        plans.MapGet("/{code}", async (
            string code,
            ITariffService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetPlanAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsRead);

        plans.MapPost("", async (
            CreateRatePlanRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePlanAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/tariffs/plans/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        plans.MapPut("/{code}", async (
            string code,
            UpdateRatePlanRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePlanAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        plans.MapPost("/{code}/set-default", async (
            string code,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPlanDefaultAsync(code, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        plans.MapPost("/{code}/activate", async (
            string code,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPlanActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        plans.MapPost("/{code}/deactivate", async (
            string code,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPlanActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);
    }

    private static void MapPeriodEndpoints(RouteGroupBuilder api)
    {
        var periods = api.MapGroup("/tariffs/plans/{code}/periods")
            .WithTags("Tariffs");

        periods.MapGet("", async (
            string code,
            string? roomTypeCode,
            ITariffService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPeriodsAsync(code, roomTypeCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsRead);

        periods.MapPost("", async (
            string code,
            CreateRatePeriodRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddPeriodAsync(code, request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/tariffs/plans/{result.Value.RatePlanCode}/periods/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        periods.MapPut("/{periodId:guid}", async (
            string code,
            Guid periodId,
            UpdateRatePeriodRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePeriodAsync(code, periodId, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        periods.MapDelete("/{periodId:guid}", async (
            string code,
            Guid periodId,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeletePeriodAsync(code, periodId, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);
    }

    private static void MapConventionEndpoints(RouteGroupBuilder api)
    {
        var conventions = api.MapGroup("/tariffs/conventions")
            .WithTags("Tariffs");

        conventions.MapGet("", async (
            string? customerCode,
            bool? includeInactive,
            ITariffService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListConventionsAsync(customerCode, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(TariffsRead);

        conventions.MapGet("/{id:guid}", async (
            Guid id,
            ITariffService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetConventionAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsRead);

        conventions.MapPost("", async (
            CreateCustomerConventionRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateConventionAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/tariffs/conventions/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        conventions.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerConventionRequest request,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateConventionAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        conventions.MapPost("/{id:guid}/activate", async (
            Guid id,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetConventionActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);

        conventions.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ITariffService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetConventionActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(TariffsWrite);
    }

    private static void MapResolveEndpoint(RouteGroupBuilder api)
    {
        // Diagnostic mirror of ITariffResolutionService.ResolveAsync: lets the front desk (and
        // the tariffs screen's "test a night" widget) check what a night would cost before any
        // reservation exists. Read permission only - resolving mutates nothing.
        api.MapGet("/tariffs/resolve", async (
            string hotelUnitCode,
            string roomTypeCode,
            DateOnly night,
            string? customerCode,
            ITariffResolutionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ResolveAsync(hotelUnitCode, roomTypeCode, night, customerCode, cancellationToken);
            return result.ToHttpResult();
        }).WithTags("Tariffs").RequireAuthorization(TariffsRead);
    }
}
