using RaqmiSystem.Application.Sync;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module 29 - Registre des postes &amp; erreurs clients. Quatre endpoints : deux d'alimentation,
/// deux de consultation.
///
/// ASYMETRIE DE PERMISSION, ET ELLE EST VOULUE : les deux POST n'exigent qu'une authentification
/// NUE, alors que les deux GET exigent sync.read. La raison est concrete : les postes a surveiller
/// sont les machines de reception et de caisse, tenues par des profils qui n'auront jamais de
/// droit d'administration. Exiger sync.read pour se declarer produirait un registre vide de
/// precisement ce qu'il doit montrer. En echange, ce que ces deux routes peuvent ecrire est borne
/// de facon stricte cote service : un lot d'erreurs est plafonne, une entree deja connue est
/// ignoree, et rien de metier n'y transite.
///
/// CE QUE CE MODULE N'EST PAS : il ne synchronise rien. Tous les postes ecrivent dans la meme base
/// PostgreSQL a travers cette meme API, il n'y a donc aucun etat divergent a reconcilier. Aucune
/// file de rejeu n'existe ici et il ne doit jamais y en avoir : rejouer une ecriture metier sur des
/// routes sans cle d'idempotence produirait des doublons d'encaissement.
/// </summary>
internal static class SyncEndpoints
{
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder api)
    {
        var sync = api.MapGroup("/sync")
            .WithTags("Supervision des postes");

        // Le StationId voyage dans le CORPS et n'est pas authentifie : un poste peut donc declarer
        // l'identite qu'il veut. C'est assume - il s'agit d'un inventaire d'exploitation, jamais
        // d'une piece justificative. Le nom d'utilisateur, lui, est pris du jeton par le service et
        // non du corps, de sorte qu'un poste ne peut pas attribuer son activite a autrui.
        sync.MapPost("/stations/heartbeat", async (
            WorkstationHeartbeatRequest request,
            ISyncSupervisionService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.HeartbeatAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization();

        sync.MapPost("/stations/failures", async (
            ReportWorkstationFailuresRequest request,
            ISyncSupervisionService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReportFailuresAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization();

        sync.MapGet("/stations", async (
            ISyncSupervisionService service,
            CancellationToken cancellationToken,
            bool includeAllKnown = false) =>
        {
            var result = await service.GetRegistryAsync(includeAllKnown, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.SyncRead);

        sync.MapGet("/failures", async (
            ISyncSupervisionService service,
            CancellationToken cancellationToken,
            int maxItems = 100) =>
        {
            var result = await service.GetFailuresAsync(maxItems, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.SyncRead);

        return api;
    }
}
