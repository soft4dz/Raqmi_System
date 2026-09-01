using System.Security.Claims;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Bibliotheque KPI : le catalogue, le tableau de bord, le comparatif inter-unites, les
/// alertes, l'historique et le parametrage, derriere UN SEUL groupe de routes "/kpis".
///
/// LECTURE : la permission d'entree est la cle DEJA existante
/// <see cref="PermissionCatalog.DashboardRead"/> - aucune cle nouvelle n'est creee pour lire des
/// indicateurs. Le filtrage FIN se fait ensuite dans le service, indicateur par indicateur, a
/// partir des permissions REELLES du jeton (<see cref="BuildAccessContext"/>) : un profil sans
/// hr.read n'obtient jamais la masse salariale, meme deguisee en pourcentage, et la reponse dit
/// combien de lignes elle ne montre pas.
///
/// ECRITURE : les trois actes de parametrage (seuils, rattachement de comptes, instantanes)
/// exigent <see cref="PermissionCatalog.KpiAdmin"/> - des actes de gouvernance, audites par le
/// service.
///
/// PARAMETRES COMMUNS des routes de calcul : from et to (obligatoires, le binding minimal-API
/// repond 400 si l'une manque), unitId (le CODE de l'unite - l'identite d'une unite dans tout
/// le depot est son code, il n'existe pas d'identifiant numerique), departmentId (code de
/// departement RH, ne restreint que la famille RH - la seule dont la donnee porte un
/// departement), dsoMethod (Simple ou CountBack), compareToPreviousYear et compareToBudget.
/// </summary>
internal static class KpiEndpoints
{
    public static RouteGroupBuilder MapKpiEndpoints(this RouteGroupBuilder api)
    {
        var kpis = api.MapGroup("/kpis")
            .WithTags("Kpi");

        // ------------------------------------------------------------------ Lecture
        kpis.MapGet("/", async (
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCatalogAsync(BuildAccessContext(user), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        kpis.MapGet("/dashboard", async (
            DateOnly from,
            DateOnly to,
            string? unitId,
            string? departmentId,
            string? dsoMethod,
            bool? compareToPreviousYear,
            bool? compareToBudget,
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildQuery(
                from, to, unitId, departmentId, dsoMethod, compareToPreviousYear, compareToBudget,
                out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetDashboardAsync(query, BuildAccessContext(user), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        kpis.MapGet("/compare", async (
            DateOnly from,
            DateOnly to,
            string? dsoMethod,
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildQuery(from, to, null, null, dsoMethod, null, null, out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetComparisonAsync(query, BuildAccessContext(user), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        kpis.MapGet("/alerts", async (
            DateOnly from,
            DateOnly to,
            string? unitId,
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildQuery(from, to, unitId, null, null, null, null, out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetAlertsAsync(query, BuildAccessContext(user), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        // Les deux routes parametrees par un code viennent APRES les routes nommees : "/compare"
        // ou "/alerts" ne doivent jamais etre captures comme un code d'indicateur.
        kpis.MapGet("/{code}", async (
            string code,
            DateOnly from,
            DateOnly to,
            string? unitId,
            string? dsoMethod,
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryBuildQuery(from, to, unitId, null, dsoMethod, null, null, out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetMeasureAsync(code, query, BuildAccessContext(user), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        kpis.MapGet("/{code}/history", async (
            string code,
            DateOnly from,
            DateOnly to,
            string? unitId,
            ClaimsPrincipal user,
            IKpiService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetHistoryAsync(
                code, unitId, from, to, BuildAccessContext(user), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        // ----------------------------------------------------------------- Parametrage
        kpis.MapGet("/thresholds", async (
            string? kpiCode,
            string? unitId,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetThresholdsAsync(kpiCode, unitId, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPut("/thresholds", async (
            SaveKpiThresholdRequest request,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SaveThresholdAsync(
                request, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/thresholds/{id:guid}/activate", async (
            Guid id,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetThresholdActiveAsync(
                id, true, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/thresholds/{id:guid}/deactivate", async (
            Guid id,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetThresholdActiveAsync(
                id, false, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapGet("/account-mappings", async (
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAccountMappingsAsync(cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPut("/account-mappings", async (
            SaveKpiAccountMappingRequest request,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SaveAccountMappingAsync(
                request, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/account-mappings/{id:guid}/activate", async (
            Guid id,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetAccountMappingActiveAsync(
                id, true, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/account-mappings/{id:guid}/deactivate", async (
            Guid id,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetAccountMappingActiveAsync(
                id, false, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/snapshots", async (
            CaptureKpiSnapshotsRequest request,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CaptureSnapshotsAsync(
                request, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        kpis.MapPost("/snapshots/close", async (
            CloseKpiSnapshotsRequest request,
            HttpContext httpContext,
            IKpiAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CloseSnapshotsAsync(
                request, httpContext.ToOperationContext(), cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KpiAdmin);

        return api;
    }

    /// <summary>
    /// Le contexte d'acces du profil connecte, construit a partir des revendications REELLES du
    /// jeton - jamais d'un role suppose. C'est ce contexte que le service applique indicateur
    /// par indicateur ; les politiques d'autorisation des routes ne gardent que la porte
    /// d'entree.
    /// </summary>
    private static KpiAccessContext BuildAccessContext(ClaimsPrincipal user)
    {
        return new KpiAccessContext(
            user.Claims
                .Where(claim => claim.Type == SecurityClaimTypes.Permission)
                .Select(claim => claim.Value));
    }

    /// <summary>
    /// Assemble la requete de calcul. Une methode DSO illisible est un 400 explicite plutot
    /// qu'un repli silencieux sur la methode simple, qui ferait lire au controleur un delai
    /// qu'il n'a pas demande.
    /// </summary>
    private static bool TryBuildQuery(
        DateOnly from,
        DateOnly to,
        string? unitId,
        string? departmentId,
        string? dsoMethod,
        bool? compareToPreviousYear,
        bool? compareToBudget,
        out KpiQuery query,
        out string error)
    {
        query = null!;
        error = string.Empty;

        var method = KpiDsoMethod.Simple;

        if (!string.IsNullOrWhiteSpace(dsoMethod)
            && !Enum.TryParse(dsoMethod.Trim(), ignoreCase: true, out method))
        {
            error = "La methode DSO doit valoir Simple ou CountBack.";
            return false;
        }

        query = new KpiQuery(
            from,
            to,
            string.IsNullOrWhiteSpace(unitId) ? null : unitId.Trim(),
            string.IsNullOrWhiteSpace(departmentId) ? null : departmentId.Trim(),
            method,
            compareToPreviousYear ?? true,
            compareToBudget ?? true);

        return true;
    }
}
