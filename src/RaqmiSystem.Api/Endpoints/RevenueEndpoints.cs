using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Api.Endpoints;

internal static class RevenueEndpoints
{
    public static RouteGroupBuilder MapRevenueEndpoints(this RouteGroupBuilder api)
    {
        var revenue = api.MapGroup("/revenue/daily")
            .WithTags("Daily revenue");

        revenue.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? status,
            IDailyRevenueService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseStatus(status, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListAsync(from, to, hotelUnitCode, parsedStatus, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.RevenueRead);

        revenue.MapGet("/summary", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? status,
            IDailyRevenueService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseStatus(status, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetSummaryAsync(from, to, hotelUnitCode, parsedStatus, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueRead);

        revenue.MapGet("/{id:guid}", async (
            Guid id,
            IDailyRevenueService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueRead);

        revenue.MapPost("", async (
            CreateDailyRevenueRequest request,
            IDailyRevenueService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/revenue/daily/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueWrite);

        revenue.MapPut("/{id:guid}", async (
            Guid id,
            UpdateDailyRevenueRequest request,
            IDailyRevenueService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueWrite);

        revenue.MapPost("/{id:guid}/submit", async (
            Guid id,
            IDailyRevenueService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueWrite);

        revenue.MapPost("/{id:guid}/validate", async (
            Guid id,
            IDailyRevenueService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ValidateAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueValidate);

        revenue.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectDailyRevenueRequest request,
            IDailyRevenueService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.RevenueValidate);

        return api;
    }

    private static bool TryParseStatus(string? status, out DailyRevenueStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<DailyRevenueStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Daily revenue status must be Draft, Submitted, Validated or Rejected.";
        return false;
    }
}
