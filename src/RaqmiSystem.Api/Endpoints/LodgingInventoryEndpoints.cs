using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10 - ce qui commande l'INVENTAIRE avant toute vente : blocages hors service (OOO/OOS),
/// regles d'exploitation de l'unite, restrictions de vente et autorisations de surreservation.
///
/// PERMISSIONS : "lodging.read" pour tous les GET ; "lodging.manage_rooms" pour les blocages de
/// chambres, qui sont un acte de parc ; "lodging.manage_rates" pour les regles de vente, la
/// surreservation et la politique d'unite, qui engagent le commercial et pas le parc.
/// </summary>
internal static class LodgingInventoryEndpoints
{
    public static RouteGroupBuilder MapLodgingInventoryEndpoints(this RouteGroupBuilder api)
    {
        MapRoomBlockEndpoints(api);
        MapPolicyEndpoints(api);
        MapRestrictionEndpoints(api);
        MapOverbookingEndpoints(api);
        return api;
    }

    private static void MapRoomBlockEndpoints(RouteGroupBuilder api)
    {
        var blocks = api.MapGroup("/lodging/room-blocks").WithTags("RoomBlocks");

        blocks.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            string? kind,
            bool? includeClosed,
            ILodgingInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            if (!TryParseKind(kind, out var parsedKind, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListRoomBlocksAsync(
                hotelUnitCode,
                from,
                to,
                parsedKind,
                includeClosed == true,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        blocks.MapGet("/{id:guid}", async (
            Guid id,
            ILodgingInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomBlockAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        blocks.MapPost("", async (
            string? hotelUnitCode,
            CreateRoomBlockRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.CreateRoomBlockAsync(
                hotelUnitCode,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/room-blocks/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);

        blocks.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRoomBlockRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomBlockAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);

        blocks.MapPost("/{id:guid}/close", async (
            Guid id,
            CloseRoomBlockRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CloseRoomBlockAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);

        blocks.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelRoomBlockRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelRoomBlockAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);

        // Raccourcis PAR CHAMBRE, la forme sous laquelle l'exploitation pense reellement le geste :
        // "je mets la 214 hors service", pas "je cree un blocage portant sur la 214".
        var rooms = api.MapGroup("/lodging/rooms").WithTags("RoomBlocks");

        rooms.MapPost("/{id:guid}/out-of-order", async (
            Guid id,
            string? hotelUnitCode,
            RoomOutOfServiceRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await CreateBlockForRoomAsync(
                id,
                hotelUnitCode,
                request,
                RoomBlockKind.OutOfOrder,
                service,
                httpContext,
                cancellationToken);
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);

        rooms.MapPost("/{id:guid}/out-of-service", async (
            Guid id,
            string? hotelUnitCode,
            RoomOutOfServiceRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await CreateBlockForRoomAsync(
                id,
                hotelUnitCode,
                request,
                RoomBlockKind.OutOfService,
                service,
                httpContext,
                cancellationToken);
        }).RequireAuthorization(PermissionCatalog.LodgingManageRooms);
    }

    private static async Task<IResult> CreateBlockForRoomAsync(
        Guid roomId,
        string? hotelUnitCode,
        RoomOutOfServiceRequest request,
        RoomBlockKind kind,
        ILodgingInventoryService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
        }

        var result = await service.CreateRoomBlockAsync(
            hotelUnitCode,
            new CreateRoomBlockRequest(
                roomId,
                kind,
                request.StartDate,
                request.EndDate,
                request.Reason,
                request.Category,
                request.MaintenanceReference,
                request.Comment),
            httpContext.ToOperationContext(),
            cancellationToken);

        return result.Succeeded && result.Value is not null
            ? Results.Created($"/api/v1/lodging/room-blocks/{result.Value.Id}", result.Value)
            : result.ToHttpResult();
    }

    private static void MapPolicyEndpoints(RouteGroupBuilder api)
    {
        var policies = api.MapGroup("/lodging/policy").WithTags("LodgingPolicy");

        policies.MapGet("", async (
            string? hotelUnitCode,
            ILodgingInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetPolicyAsync(hotelUnitCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        policies.MapPut("", async (
            string? hotelUnitCode,
            SaveLodgingPolicyRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.SavePolicyAsync(
                hotelUnitCode,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);
    }

    private static void MapRestrictionEndpoints(RouteGroupBuilder api)
    {
        var restrictions = api.MapGroup("/lodging/restrictions").WithTags("Restrictions");

        restrictions.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            bool? includeInactive,
            ILodgingInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListRestrictionsAsync(
                hotelUnitCode,
                from,
                to,
                includeInactive == true,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        restrictions.MapPost("", async (
            SaveRateRestrictionRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRestrictionAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/restrictions/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        restrictions.MapPut("/{id:guid}", async (
            Guid id,
            SaveRateRestrictionRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRestrictionAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        restrictions.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRestrictionActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        restrictions.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRestrictionActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);
    }

    private static void MapOverbookingEndpoints(RouteGroupBuilder api)
    {
        var overbooking = api.MapGroup("/lodging/overbooking").WithTags("Overbooking");

        overbooking.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            bool? includeInactive,
            ILodgingInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListOverbookingAsync(
                hotelUnitCode,
                from,
                to,
                includeInactive == true,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingRead);

        overbooking.MapPost("", async (
            SaveOverbookingAllowanceRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateOverbookingAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/lodging/overbooking/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        overbooking.MapPut("/{id:guid}", async (
            Guid id,
            SaveOverbookingAllowanceRequest request,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateOverbookingAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        overbooking.MapPost("/{id:guid}/activate", async (
            Guid id,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetOverbookingActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);

        overbooking.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ILodgingInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetOverbookingActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingManageRates);
    }

    private static bool TryParseKind(string? kind, out RoomBlockKind? parsed, out string error)
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(kind))
        {
            return true;
        }

        if (Enum.TryParse<RoomBlockKind>(kind.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value))
        {
            parsed = value;
            return true;
        }

        error = "La nature du blocage doit valoir OutOfOrder ou OutOfService.";
        return false;
    }
}

/// <summary>
/// Corps des raccourcis /rooms/{id}/out-of-order et /rooms/{id}/out-of-service : la chambre vient
/// de l'URL, la nature du blocage vient de la route.
/// </summary>
public sealed record RoomOutOfServiceRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    RoomBlockCategory Category = RoomBlockCategory.Unspecified,
    string? MaintenanceReference = null,
    string? Comment = null);
