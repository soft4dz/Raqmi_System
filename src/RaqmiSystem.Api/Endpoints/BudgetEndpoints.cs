using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder api)
    {
        MapBudgetPlanEndpoints(api);
        MapBudgetVarianceEndpoints(api);
        return api;
    }

    private static void MapBudgetPlanEndpoints(RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/budget/plans")
            .WithTags("Budget plans");

        plans.MapGet("", async (
            int? year,
            string? hotelUnitCode,
            string? status,
            IBudgetService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseStatus(status, out var parsedStatus, out var statusError))
            {
                return Results.BadRequest(new ErrorResponse(statusError));
            }

            if (year.HasValue && year.Value is < 2000 or > 2999)
            {
                return Results.BadRequest(new ErrorResponse("Year must be between 2000 and 2999."));
            }

            var result = await service.ListPlansAsync(year, hotelUnitCode, parsedStatus, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetRead);

        plans.MapGet("/{id:guid}", async (
            Guid id,
            IBudgetService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetPlanAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetRead);

        plans.MapPost("", async (
            CreateBudgetPlanRequest request,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePlanAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/budget/plans/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetManage);

        plans.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBudgetPlanRequest request,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePlanAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetManage);

        plans.MapPut("/{id:guid}/lines", async (
            Guid id,
            ReplaceBudgetLinesRequest request,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReplacePlanLinesAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetManage);

        plans.MapPost("/{id:guid}/lines", async (
            Guid id,
            BudgetLineRequest request,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPlanLineAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetManage);

        plans.MapDelete("/{id:guid}/lines/{lineId:guid}", async (
            Guid id,
            Guid lineId,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RemovePlanLineAsync(id, lineId, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetManage);

        // Approval is a distinct engaging act, not an edit: it freezes the budget the direction
        // will be measured against, so it carries its own permission key.
        plans.MapPost("/{id:guid}/approve", async (
            Guid id,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApprovePlanAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetApprove);

        plans.MapPost("/{id:guid}/close", async (
            Guid id,
            IBudgetService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ClosePlanAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetApprove);
    }

    private static void MapBudgetVarianceEndpoints(RouteGroupBuilder api)
    {
        var budget = api.MapGroup("/budget")
            .WithTags("Budget variance");

        budget.MapGet("/variance", async (
            int? year,
            string? hotelUnitCode,
            int? month,
            IBudgetService service,
            CancellationToken cancellationToken) =>
        {
            if (!year.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("The year query parameter is required."));
            }

            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("The hotelUnitCode query parameter is required."));
            }

            if (month.HasValue && month.Value is < 1 or > 12)
            {
                return Results.BadRequest(new ErrorResponse("Month must be between 1 and 12."));
            }

            var result = await service.GetVarianceAsync(year.Value, hotelUnitCode, month, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBudgetRead);
    }

    private static bool TryParseStatus(string? status, out BudgetStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<BudgetStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Budget status must be Draft, Approved or Closed.";
        return false;
    }
}
