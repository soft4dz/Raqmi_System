using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 10 - l'exploitation quotidienne : date metier, previsionnel, planning graphique, tableaux
/// d'arrivees et de departs, clients presents, no-shows et night audit.
///
/// PERMISSIONS : "lodging.read" pour tout ce qui se lit ; "lodging.noshow" pour APPLIQUER un
/// balayage de non-presentations (le lire reste une lecture) ; "lodging.night_audit" pour passer
/// le night audit.
/// </summary>
internal static class LodgingOperationsEndpoints
{
    public static RouteGroupBuilder MapLodgingOperationsEndpoints(this RouteGroupBuilder api)
    {
        var lodging = api.MapGroup("/lodging").WithTags("LodgingOperations");

        lodging.MapGet("/business-date", async (
            string? hotelUnitCode,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetBusinessDateAsync(hotelUnitCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapGet("/forecast", async (
            string? hotelUnitCode,
            DateOnly? from,
            int? days,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetForecastAsync(
                hotelUnitCode,
                from ?? DateOnly.FromDateTime(DateTime.UtcNow),
                days ?? 30,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapGet("/tape-chart", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await service.GetTapeChartAsync(
                hotelUnitCode,
                start,
                to ?? start.AddDays(14),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapGet("/arrivals", async (
            string? hotelUnitCode,
            DateOnly? date,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetArrivalsAsync(hotelUnitCode, date, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapGet("/departures", async (
            string? hotelUnitCode,
            DateOnly? date,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetDeparturesAsync(hotelUnitCode, date, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapGet("/in-house", async (
            string? hotelUnitCode,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.GetInHouseAsync(hotelUnitCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        // Le rapport des non-presentations se LIT sans droit particulier : c'est une liste de
        // candidats. L'appliquer bascule des dossiers et declenche des penalites - d'ou la route
        // separee et la cle dediee.
        lodging.MapGet("/no-shows", async (
            string? hotelUnitCode,
            DateOnly? businessDate,
            ILodgingOperationsService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.SweepNoShowsAsync(
                hotelUnitCode,
                businessDate,
                apply: false,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        lodging.MapPost("/no-shows/apply", async (
            string? hotelUnitCode,
            DateOnly? businessDate,
            ILodgingOperationsService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.SweepNoShowsAsync(
                hotelUnitCode,
                businessDate,
                apply: true,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingReservationNoshow);

        var nightAudit = api.MapGroup("/lodging/night-audit").WithTags("NightAudit");

        nightAudit.MapGet("", async (
            string? hotelUnitCode,
            DateOnly? from,
            DateOnly? to,
            ILodgingOperationsService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hotelUnitCode))
            {
                return Results.BadRequest(new ErrorResponse("Le code de l'unite hoteliere est requis."));
            }

            var result = await service.ListNightAuditRunsAsync(hotelUnitCode, from, to, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingFrontOfficeRead);

        nightAudit.MapPost("/run", async (
            RunNightAuditRequest request,
            ILodgingOperationsService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RunNightAuditAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.LodgingNightAuditExecute);

        return api;
    }
}
