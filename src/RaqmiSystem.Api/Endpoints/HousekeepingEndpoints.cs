using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Housekeeping module (module 10.2): room board, daily cleaning sheet and team planning,
/// inspection loop, and the minibar price list with the consumptions it bills onto folios.
///
/// Permissions (policy names are the permission keys registered in Program.cs from
/// PermissionCatalog):
///   - "housekeeping.read"    every GET;
///   - "housekeeping.write"   planning and running the sheet (generate, create, assign, start,
///                            complete, cancel), declaring a room condition, the minibar price
///                            list, and recording a consumption;
///   - "housekeeping.inspect" the supervisor verdict on a finished room - the ONE act the floor
///                            attendant who cleaned it must not be able to sign off alone.
/// </summary>
internal static class HousekeepingEndpoints
{
    public static RouteGroupBuilder MapHousekeepingEndpoints(this RouteGroupBuilder api)
    {
        MapBoardEndpoints(api);
        MapTaskEndpoints(api);
        MapMinibarEndpoints(api);
        return api;
    }

    private static void MapBoardEndpoints(RouteGroupBuilder api)
    {
        var housekeeping = api.MapGroup("/housekeeping")
            .WithTags("Housekeeping");

        housekeeping.MapGet("/board", async (
            string? hotelUnitCode,
            DateOnly? date,
            IHousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Hotel unit code is required."));
            }

            if (!date.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("The date is required."));
            }

            var result = await service.GetRoomBoardAsync(hotelUnitCode, date.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        housekeeping.MapGet("/day-sheet", async (
            string? hotelUnitCode,
            DateOnly? date,
            IHousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Hotel unit code is required."));
            }

            if (!date.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("The date is required."));
            }

            var result = await service.GetDaySheetAsync(hotelUnitCode, date.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        housekeeping.MapPost("/rooms/{roomId:guid}/condition", async (
            Guid roomId,
            SetRoomConditionRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomConditionAsync(
                roomId,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);
    }

    private static void MapTaskEndpoints(RouteGroupBuilder api)
    {
        var tasks = api.MapGroup("/housekeeping/tasks")
            .WithTags("HousekeepingTasks");

        tasks.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? status,
            string? assignedTo,
            IHousekeepingService service,
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

            var result = await service.ListTasksAsync(
                from,
                to,
                hotelUnitCode,
                parsedStatus,
                assignedTo,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        tasks.MapGet("/{id:guid}", async (
            Guid id,
            IHousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetTaskAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        tasks.MapPost("", async (
            CreateHousekeepingTaskRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateTaskAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/housekeeping/tasks/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        tasks.MapPost("/generate", async (
            GenerateHousekeepingTasksRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GenerateDayTasksAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        tasks.MapPost("/{id:guid}/assign", async (
            Guid id,
            AssignHousekeepingTaskRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AssignTaskAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        tasks.MapPost("/{id:guid}/start", async (
            Guid id,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.StartTaskAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        tasks.MapPost("/{id:guid}/complete", async (
            Guid id,
            CompleteHousekeepingTaskRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteTaskAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        // The one act reserved to a supervisor: signing a room off (or refusing it) is what
        // makes the inspection a control rather than a self-declaration.
        tasks.MapPost("/{id:guid}/inspect", async (
            Guid id,
            InspectHousekeepingTaskRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.InspectTaskAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingInspect);

        tasks.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelHousekeepingTaskRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelTaskAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);
    }

    private static void MapMinibarEndpoints(RouteGroupBuilder api)
    {
        var items = api.MapGroup("/housekeeping/minibar/items")
            .WithTags("Minibar");

        items.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            IHousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListMinibarItemsAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        items.MapPost("", async (
            CreateMinibarItemRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateMinibarItemAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/housekeeping/minibar/items/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        items.MapPut("/{id:guid}", async (
            Guid id,
            UpdateMinibarItemRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateMinibarItemAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        items.MapPost("/{id:guid}/activate", async (
            Guid id,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetMinibarItemActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        items.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetMinibarItemActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);

        var consumptions = api.MapGroup("/housekeeping/minibar/consumptions")
            .WithTags("Minibar");

        consumptions.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            Guid? reservationId,
            IHousekeepingService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.ListMinibarConsumptionsAsync(
                from,
                to,
                hotelUnitCode,
                reservationId,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.HousekeepingRead);

        // Records the consumption AND bills it on the folio, in one database transaction. The
        // reservation must be checked in - the guard is enforced by the lodging folio path this
        // goes through, not re-implemented here.
        consumptions.MapPost("", async (
            RecordMinibarConsumptionRequest request,
            IHousekeepingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RecordMinibarConsumptionAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/housekeeping/minibar/consumptions/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.HousekeepingWrite);
    }

    private static bool TryParseStatus(
        string? status,
        out HousekeepingTaskStatus? parsedStatus,
        out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<HousekeepingTaskStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Housekeeping task status must be Pending, InProgress, Cleaned, Inspected, Rejected or Cancelled.";
        return false;
    }
}
