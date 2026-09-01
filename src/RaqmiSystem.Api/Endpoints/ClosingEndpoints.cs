using RaqmiSystem.Application.Closing;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class ClosingEndpoints
{

    public static RouteGroupBuilder MapClosingEndpoints(this RouteGroupBuilder api)
    {
        var closing = api.MapGroup("/closing/daily")
            .WithTags("Daily closing");

        closing.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            IDailyClosingService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.ListAsync(from, to, hotelUnitCode, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.LodgingClosingRead);

        closing.MapGet("/{businessDate}/{unitCode}", async (
            DateOnly businessDate,
            string unitCode,
            IDailyClosingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(businessDate, unitCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingClosingRead);

        closing.MapPost("/close", async (
            CloseBusinessDayRequest request,
            IDailyClosingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CloseAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created(
                    $"/api/v1/closing/daily/{result.Value.BusinessDate:yyyy-MM-dd}/{result.Value.HotelUnitCode}",
                    result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingClosingClose);

        closing.MapPost("/{id:guid}/reopen", async (
            Guid id,
            ReopenDailyClosingRequest request,
            IDailyClosingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReopenAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingClosingReopen);

        return api;
    }
}
