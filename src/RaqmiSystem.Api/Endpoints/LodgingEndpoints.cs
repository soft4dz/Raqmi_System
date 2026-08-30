using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Lodging module (module 10): room types, rooms, reservations, folios and occupancy.
///
/// Permissions (policy names are the permission keys registered in Program.cs from
/// PermissionCatalog):
///   - "lodging.read"    every GET;
///   - "lodging.write"   the property setup (room types, rooms) and the reservation lifecycle
///                       decisions (create, cancel, no-show);
///   - "lodging.checkin" the front-desk counter operations: check-in, check-out and folio lines.
/// </summary>
internal static class LodgingEndpoints
{
    public static RouteGroupBuilder MapLodgingEndpoints(this RouteGroupBuilder api)
    {
        MapRoomTypeEndpoints(api);
        MapRoomEndpoints(api);
        MapAvailabilityEndpoints(api);
        MapReservationEndpoints(api);
        MapOccupancyEndpoints(api);
        MapFrontDeskEndpoints(api);
        return api;
    }

    /// <summary>
    /// The dates-first booking flow of a PMS: GET /lodging/availability lists every bookable
    /// room of the unit over [from, to) for the party size, priced night by night (with the
    /// customer's convention when customerCode is passed). Free rooms the tariff module cannot
    /// price come back flagged HasRate=false rather than hidden.
    /// </summary>
    private static void MapAvailabilityEndpoints(RouteGroupBuilder api)
    {
        var availability = api.MapGroup("/lodging/availability")
            .WithTags("Availability");

        availability.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            int? guests,
            string? customerCode,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Hotel unit code is required."));
            }

            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("Both from and to dates are required."));
            }

            var result = await service.GetAvailabilityAsync(
                hotelUnitCode,
                from.Value,
                to.Value,
                guests ?? 1,
                customerCode,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);
    }

    /// <summary>
    /// The counter screen: GET /lodging/front-desk returns the arrivals, departures (with folio
    /// balances), overdue lists, in-house count and occupancy of one unit for one day.
    /// </summary>
    private static void MapFrontDeskEndpoints(RouteGroupBuilder api)
    {
        var frontDesk = api.MapGroup("/lodging/front-desk")
            .WithTags("FrontDesk");

        frontDesk.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? date,
            ILodgingService service,
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

            var result = await service.GetFrontDeskAsync(hotelUnitCode, date.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);
    }

    private static void MapRoomTypeEndpoints(RouteGroupBuilder api)
    {
        var roomTypes = api.MapGroup("/lodging/room-types")
            .WithTags("RoomTypes");

        roomTypes.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListRoomTypesAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        roomTypes.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomTypeAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        roomTypes.MapPost("", async (
            CreateRoomTypeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRoomTypeAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/room-types/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        roomTypes.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRoomTypeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomTypeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        roomTypes.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomTypeActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        roomTypes.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomTypeActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);
    }

    private static void MapRoomEndpoints(RouteGroupBuilder api)
    {
        var rooms = api.MapGroup("/lodging/rooms")
            .WithTags("Rooms");

        rooms.MapGet("", async (
            string? hotelUnitCode,
            bool? includeInactive,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListRoomsAsync(hotelUnitCode, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        rooms.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        rooms.MapPost("", async (
            CreateRoomRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRoomAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/rooms/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        rooms.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRoomRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        rooms.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        rooms.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);
    }

    private static void MapReservationEndpoints(RouteGroupBuilder api)
    {
        var reservations = api.MapGroup("/lodging/reservations")
            .WithTags("Reservations");

        reservations.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? status,
            string? customerCode,
            ILodgingService service,
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

            var result = await service.ListReservationsAsync(
                from,
                to,
                hotelUnitCode,
                parsedStatus,
                customerCode,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        reservations.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetReservationAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        reservations.MapPost("", async (
            CreateReservationRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateReservationAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/reservations/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        reservations.MapPost("/{id:guid}/check-in", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CheckInAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckin);

        reservations.MapPost("/{id:guid}/check-out", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CheckOutAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckin);

        reservations.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelReservationRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelReservationAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        reservations.MapPost("/{id:guid}/no-show", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.MarkNoShowAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingWrite);

        reservations.MapGet("/{id:guid}/folio", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetFolioAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        reservations.MapPost("/{id:guid}/folio/charges", async (
            Guid id,
            AddFolioChargeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddFolioChargeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckin);
    }

    private static void MapOccupancyEndpoints(RouteGroupBuilder api)
    {
        var occupancy = api.MapGroup("/lodging/occupancy")
            .WithTags("Occupancy");

        occupancy.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Hotel unit code is required."));
            }

            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("Both from and to dates are required."));
            }

            var result = await service.GetOccupancyAsync(hotelUnitCode, from.Value, to.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);
    }

    private static bool TryParseStatus(string? status, out ReservationStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<ReservationStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Reservation status must be Booked, CheckedIn, CheckedOut, Cancelled or NoShow.";
        return false;
    }
}
