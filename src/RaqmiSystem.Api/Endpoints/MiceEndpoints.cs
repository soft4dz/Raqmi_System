using RaqmiSystem.Application.Mice;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10.6 - Groupes &amp; MICE. Les six fonctions annoncees au catalogue sont desormais la :
/// espaces de reception, evenements, devis, BEO, facturation evenementielle, puis allotements et
/// rooming lists.
///
/// PERMISSIONS, ET LEUR LOGIQUE : mice.read pour consulter, mice.write pour agir. Deux familles de
/// routes en exigent une SECONDE, parce qu'elles ecrivent hors du module :
///   * facturer un evenement demande aussi invoices.write - cela cree une facture reelle ;
///   * poser un allotement ou soumettre une rooming list demande aussi lodging.write - cela gele
///     ou consomme de l'inventaire chambres.
/// Sans ces secondes exigences, mice.write serait devenu un chemin detourne vers la facturation et
/// vers l'inventaire du PMS.
/// </summary>
internal static class MiceEndpoints
{
    public static RouteGroupBuilder MapMiceEndpoints(this RouteGroupBuilder api)
    {
        var mice = api.MapGroup("/mice").WithTags("Groupes & MICE");

        // ------------------------------- Espaces de reception -------------------------------

        mice.MapGet("/spaces", async (
            IMiceService service,
            CancellationToken cancellationToken,
            string? hotelUnitCode = null,
            bool includeInactive = false) =>
        {
            var result = await service.ListFunctionSpacesAsync(hotelUnitCode, includeInactive, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.MiceRead);

        mice.MapPost("/spaces/{hotelUnitCode}/{code}", async (
            string hotelUnitCode,
            string code,
            SaveFunctionSpaceRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateFunctionSpaceAsync(
                hotelUnitCode,
                code,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPut("/spaces/{hotelUnitCode}/{code}", async (
            string hotelUnitCode,
            string code,
            SaveFunctionSpaceRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateFunctionSpaceAsync(
                hotelUnitCode,
                code,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPost("/spaces/{hotelUnitCode}/{code}/activate", async (
            string hotelUnitCode,
            string code,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetFunctionSpaceActiveAsync(
                hotelUnitCode,
                code,
                true,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPost("/spaces/{hotelUnitCode}/{code}/deactivate", async (
            string hotelUnitCode,
            string code,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetFunctionSpaceActiveAsync(
                hotelUnitCode,
                code,
                false,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        // ------------------------------------ Evenements ------------------------------------

        mice.MapGet("/events", async (
            IMiceService service,
            CancellationToken cancellationToken,
            string? hotelUnitCode = null,
            DateOnly? from = null,
            DateOnly? to = null,
            string? functionSpaceCode = null,
            bool includeCancelled = false) =>
        {
            var result = await service.ListEventsAsync(
                hotelUnitCode,
                from,
                to,
                functionSpaceCode,
                includeCancelled,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.MiceRead);

        mice.MapGet("/events/{id:guid}", async (
            Guid id,
            IMiceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetEventAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceRead);

        mice.MapPost("/events", async (
            CreateEventBookingRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateEventAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPut("/events/{id:guid}", async (
            Guid id,
            UpdateEventBookingRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateEventAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        // Deplacer un evenement rejoue le garde de chevauchement : c'est une operation a part
        // entiere et non un champ de la mise a jour descriptive.
        mice.MapPost("/events/{id:guid}/reschedule", async (
            Guid id,
            RescheduleEventBookingRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RescheduleEventAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPost("/events/{id:guid}/confirm", async (
            Guid id,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmEventAsync(
                id,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPost("/events/{id:guid}/cancel", async (
            Guid id,
            CancelEventBookingRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelEventAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        // ------------------------------- Devis et BEO -------------------------------

        mice.MapPut("/events/{id:guid}/lines", async (
            Guid id,
            IReadOnlyCollection<EventBookingLineRequest> lines,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceEventLinesAsync(
                id,
                lines,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        mice.MapPut("/events/{id:guid}/schedule", async (
            Guid id,
            IReadOnlyCollection<EventScheduleItemRequest> schedule,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceEventScheduleAsync(
                id,
                schedule,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite);

        // -------------------------- Facturation evenementielle --------------------------

        // DEUX politiques exigees, et les deux doivent passer. Cette route ecrit une facture
        // reelle a travers le module Facturation : sans invoices.write, mice.write deviendrait un
        // chemin detourne permettant de creer des factures sans en avoir le droit.
        mice.MapPost("/events/{id:guid}/invoice", async (
            Guid id,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.InvoiceEventAsync(
                id,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.InvoicesWrite);

        // ==================== Allotements et rooming lists (volet GROUPES) ====================
        //
        // Ces routes touchent l'inventaire chambres : un bloc pose ici retire des chambres de la
        // vente publique. Le controle est fait par LodgingService, au meme endroit que la recherche
        // de disponibilite et que le garde de creation de reservation.

        mice.MapGet("/allotments", async (
            IMiceService service,
            CancellationToken cancellationToken,
            string? hotelUnitCode = null,
            DateOnly? from = null,
            DateOnly? to = null,
            bool includeClosed = false) =>
        {
            var result = await service.ListAllotmentsAsync(hotelUnitCode, from, to, includeClosed, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.MiceRead);

        // Poser un bloc retire des chambres de la vente : cela exige mice.write ET lodging.write.
        // Sans cette seconde exigence, un commercial pourrait geler l'inventaire chambres sans
        // avoir le droit d'y toucher par ailleurs.
        mice.MapPost("/allotments", async (
            CreateRoomAllotmentRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAllotmentAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        mice.MapPut("/allotments/{id:guid}", async (
            Guid id,
            UpdateRoomAllotmentRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAllotmentAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        mice.MapPost("/allotments/{id:guid}/confirm", async (
            Guid id,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmAllotmentAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        mice.MapPost("/allotments/{id:guid}/release", async (
            Guid id,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReleaseAllotmentAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        mice.MapPost("/allotments/{id:guid}/cancel", async (
            Guid id,
            CancelRoomAllotmentRequest request,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAllotmentAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        mice.MapGet("/allotments/{id:guid}/rooming-list", async (
            Guid id,
            IMiceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRoomingListAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceRead);

        // Soumettre une rooming list CREE des reservations : mice.write ne suffit pas, il faut
        // aussi le droit de reserver.
        mice.MapPut("/allotments/{id:guid}/rooming-list", async (
            Guid id,
            IReadOnlyCollection<RoomingListEntryRequest> entries,
            IMiceService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitRoomingListAsync(
                id,
                entries,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MiceWrite, PermissionCatalog.LodgingWrite);

        return api;
    }
}
