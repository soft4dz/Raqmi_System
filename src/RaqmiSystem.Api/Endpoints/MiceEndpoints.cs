using RaqmiSystem.Application.Mice;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10.6 - Groupes &amp; MICE, volet EVENEMENTIEL : espaces de reception, evenements, devis,
/// BEO et facturation evenementielle.
///
/// PERIMETRE PARTIEL, ET ASSUME. Le catalogue annonce six fonctions pour ce module. Les quatre qui
/// portent sur les SALLES sont ici. Les deux autres - allotements et rooming lists - portent sur
/// les CHAMBRES et n'y sont pas : un allotement retire des chambres de la vente, il devrait donc
/// etre soustrait a la disponibilite ET au garde de creation de reservation. Livrer un allotement
/// que la recherche de disponibilite ignore ferait survendre l'hotel en silence, ce qui est pire
/// que de ne pas le livrer.
///
/// PERMISSIONS : mice.read pour consulter, mice.write pour agir. La facturation d'un evenement
/// exige EN PLUS invoices.write - elle ecrit une facture reelle, et mice.write ne doit pas devenir
/// un chemin detourne pour creer des factures sans le droit de facturer.
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

        return api;
    }
}
