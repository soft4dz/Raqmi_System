using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10 - PMS hotelier : parc de chambres, disponibilite, dossiers, sejours, folios.
///
/// PERMISSIONS (les noms de politique sont les cles du PermissionCatalog enregistrees dans
/// Program.cs) :
///   - "lodging.read"          tous les GET ;
///   - "lodging.manage_rooms"  le parc : types, chambres, blocages hors service ;
///   - "lodging.reserve"       vendre : creation, walk-in, affectation, prolongation ;
///   - "lodging.checkin"       le comptoir pendant le sejour : arrivee, folios, acomptes ;
///   - "lodging.checkout"      le depart, qui exige un solde nul ;
///   - "lodging.room_move"     deplacer un client de chambre ;
///   - "lodging.change_rate"   surclasser ou declasser en facturant l'ecart ;
///   - "lodging.cancel"        annuler ;
///   - "lodging.noshow"        constater une non-presentation.
///
/// "lodging.write" reste en place et couvre le parametrage general du module : les installations
/// existantes qui l'ont accorde continuent de fonctionner, les cles fines s'ajoutent par-dessus.
/// </summary>
internal static class LodgingEndpoints
{
    public static RouteGroupBuilder MapLodgingEndpoints(this RouteGroupBuilder api)
    {
        MapRoomTypeEndpoints(api);
        MapRoomEndpoints(api);
        MapAvailabilityEndpoints(api);
        MapReservationEndpoints(api);
        MapStayEndpoints(api);
        MapFolioEndpoints(api);
        MapOccupancyEndpoints(api);
        MapFrontDeskEndpoints(api);
        return api;
    }

    /// <summary>
    /// Le flux de vente d'un PMS : GET /lodging/availability rend la disponibilite COMMERCIALE par
    /// type - ce qui est vendable sur toute la periode, au prix resolu nuit par nuit - et les
    /// chambres physiques libres pour l'affectation. Les fermetures de vente sont rendues
    /// explicitement : une periode vide sans explication ferait croire a une occupation complete.
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
            int? adults,
            int? children,
            int? infants,
            int? rooms,
            string? roomTypeCode,
            string? ratePlanCode,
            string? customerCode,
            string? channelCode,
            string? marketSegmentCode,
            bool? allowOverbooking,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("Les dates de debut et de fin sont requises."));
            }

            // La surreservation ne s'ouvre que si l'appelant en a le droit : la demander sans la
            // permission affiche l'inventaire normal plutot que de refuser la recherche, parce
            // qu'une recherche doit toujours repondre quelque chose.
            var overbooking = allowOverbooking == true
                && httpContext.User.HasPermission(PermissionCatalog.LodgingReservationOverbook);

            var result = await service.SearchAvailabilityAsync(
                new AvailabilitySearchRequest(
                    hotelUnitCode,
                    from.Value,
                    to.Value,
                    adults ?? guests ?? 1,
                    children ?? 0,
                    infants ?? 0,
                    rooms ?? 1,
                    roomTypeCode,
                    ratePlanCode,
                    customerCode,
                    marketSegmentCode,
                    channelCode,
                    overbooking),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);
    }

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
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            if (!date.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("La date est requise."));
            }

            var result = await service.GetFrontDeskAsync(hotelUnitCode, date.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);
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
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        roomTypes.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomTypeAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

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
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        roomTypes.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRoomTypeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomTypeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        roomTypes.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomTypeActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        roomTypes.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomTypeActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);
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
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        rooms.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

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
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        rooms.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRoomRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        rooms.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);

        rooms.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRoomActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRoomManage);
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
                return Results.BadRequest(new ErrorResponse("La date de debut ne peut pas etre posterieure a la date de fin."));
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
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetReservationAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapGet("/{id:guid}/detail", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetReservationDetailAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapPost("", async (
            CreateReservationRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Les deux leviers qui contournent une regle ne sont acceptes QUE si l'appelant les
            // detient. Sans cela, un poste de reception pourrait vendre au-dela de la capacite ou
            // passer outre un stop sell en cochant une case.
            var effective = request with
            {
                AllowOverbooking = request.AllowOverbooking
                    && httpContext.User.HasPermission(PermissionCatalog.LodgingReservationOverbook),
                OverrideRestrictions = request.OverrideRestrictions
                    && httpContext.User.HasPermission(PermissionCatalog.LodgingRestrictionOverride)
            };

            var result = await service.CreateReservationAsync(effective, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/reservations/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPost("/walk-in", async (
            WalkInRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var effective = request with
            {
                AllowOverbooking = request.AllowOverbooking
                    && httpContext.User.HasPermission(PermissionCatalog.LodgingReservationOverbook),
                OverrideRestrictions = request.OverrideRestrictions
                    && httpContext.User.HasPermission(PermissionCatalog.LodgingRestrictionOverride)
            };

            var result = await service.CreateWalkInAsync(effective, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/reservations/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPut("/{id:guid}", async (
            Guid id,
            UpdateReservationRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateReservationAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPost("/{id:guid}/status", async (
            Guid id,
            ChangeReservationStatusRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ChangeReservationStatusAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPost("/{id:guid}/guarantee", async (
            Guid id,
            SetGuaranteeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetGuaranteeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPost("/{id:guid}/assign-room", async (
            Guid id,
            AssignRoomRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AssignRoomAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapPost("/{id:guid}/check-in", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CheckInAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckinExecute);

        reservations.MapPost("/{id:guid}/prepare-check-out", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.PrepareCheckOutAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckoutExecute);

        reservations.MapPost("/{id:guid}/check-out", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CheckOutAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingCheckoutExecute);

        reservations.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelReservationRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelReservationAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCancel);

        reservations.MapPost("/{id:guid}/no-show", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.MarkNoShowAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationNoshow);
    }

    /// <summary>
    /// Les gestes du SEJOUR. Exposes sous /lodging/stays comme sous /lodging/reservations : un
    /// sejour et son dossier sont le meme objet, et les deux vocabulaires cohabitent dans un hotel.
    /// </summary>
    private static void MapStayEndpoints(RouteGroupBuilder api)
    {
        foreach (var prefix in new[] { "/lodging/stays", "/lodging/reservations" })
        {
            var stays = api.MapGroup(prefix).WithTags("Stays");

            stays.MapPost("/{id:guid}/room-move", async (
                Guid id,
                RoomMoveRequest request,
                ILodgingService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.MoveRoomAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
                return result.ToHttpResult();
            }).RequireAuthorization(PermissionCatalog.LodgingStayMove);

            stays.MapPost("/{id:guid}/extend", async (
                Guid id,
                ExtendStayRequest request,
                ILodgingService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var effective = request with
                {
                    AllowOverbooking = request.AllowOverbooking
                        && httpContext.User.HasPermission(PermissionCatalog.LodgingReservationOverbook),
                    OverrideRestrictions = request.OverrideRestrictions
                        && httpContext.User.HasPermission(PermissionCatalog.LodgingRestrictionOverride)
                };

                var result = await service.ExtendStayAsync(id, effective, httpContext.ToOperationContext(), cancellationToken);
                return result.ToHttpResult();
            }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

            stays.MapPost("/{id:guid}/change-room-type", async (
                Guid id,
                ChangeRoomTypeRequest request,
                ILodgingService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var effective = request with
                {
                    AllowOverbooking = request.AllowOverbooking
                        && httpContext.User.HasPermission(PermissionCatalog.LodgingReservationOverbook)
                };

                var result = await service.ChangeRoomTypeAsync(id, effective, httpContext.ToOperationContext(), cancellationToken);
                return result.ToHttpResult();
            }).RequireAuthorization(PermissionCatalog.LodgingStayChangeRate);
        }
    }

    private static void MapFolioEndpoints(RouteGroupBuilder api)
    {
        var reservations = api.MapGroup("/lodging/reservations").WithTags("Folios");

        reservations.MapGet("/{id:guid}/folio", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetFolioAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapGet("/{id:guid}/folios", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListFoliosAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapPost("/{id:guid}/folios", async (
            Guid id,
            CreateFolioRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateFolioAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        reservations.MapPost("/{id:guid}/folio/charges", async (
            Guid id,
            AddFolioChargeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddFolioChargeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        reservations.MapPost("/{id:guid}/folio/transfer", async (
            Guid id,
            TransferFolioChargeRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.TransferFolioChargeAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        // ------------------------------------- Extras -------------------------------------

        reservations.MapGet("/{id:guid}/extras", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListReservationExtrasAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapPost("/{id:guid}/extras", async (
            Guid id,
            AddReservationExtraRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddReservationExtraAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        reservations.MapDelete("/{id:guid}/extras/{extraId:guid}", async (
            Guid id,
            Guid extraId,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RemoveReservationExtraAsync(id, extraId, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCreate);

        // ------------------------------------ Acomptes ------------------------------------

        reservations.MapGet("/{id:guid}/deposits", async (
            Guid id,
            ILodgingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListDepositsAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        reservations.MapPost("/{id:guid}/deposits", async (
            Guid id,
            CreateDepositRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateDepositAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        var deposits = api.MapGroup("/lodging/deposits").WithTags("Folios");

        deposits.MapPost("/{id:guid}/pay", async (
            Guid id,
            PayDepositRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.PayDepositAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        deposits.MapPost("/{id:guid}/apply", async (
            Guid id,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApplyDepositAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        deposits.MapPost("/{id:guid}/refund", async (
            Guid id,
            CloseDepositRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RefundDepositAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFolioManage);

        deposits.MapPost("/{id:guid}/forfeit", async (
            Guid id,
            CloseDepositRequest request,
            ILodgingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ForfeitDepositAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationCancel);
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
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("Les dates de debut et de fin sont requises."));
            }

            var result = await service.GetOccupancyAsync(hotelUnitCode, from.Value, to.Value, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);
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

        error = "Le statut doit valoir Inquiry, Option, Confirmed, Guaranteed, CheckedIn, "
            + "CheckedOut, Cancelled ou NoShow.";

        return false;
    }
}
