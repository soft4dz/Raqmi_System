using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Pilotage - endpoints du dashboard PDG (24.2) UNIQUEMENT.
///
/// NOTE INTEGRATEUR : ce fichier est PARTAGE avec le chantier Cockpit DEC mene en parallele ;
/// il ne contient ici que le groupe /pilotage/group-dashboard et la methode d'extension
/// MapPilotageGroupEndpoints. A la fusion, garder une seule classe PilotageEndpoints et y
/// ranger les deux familles de routes (les methodes d'extension restent distinctes :
/// MapPilotageGroupEndpoints / celle du DEC), puis enregistrer les deux appels dans Program.cs.
///
/// Permission : chaine litterale "dashboard.read" = PermissionCatalog.DashboardRead, cle DEJA
/// existante et deja semee (direction, exploitation.control, unit.manager, reader) - aucune
/// cle nouvelle a creer ; remplacer le litteral par la constante a l'integration.
/// </summary>
internal static class PilotageEndpoints
{
    public static RouteGroupBuilder MapPilotageGroupEndpoints(this RouteGroupBuilder api)
    {
        var pilotage = api.MapGroup("/pilotage")
            .WithTags("Pilotage");

        // Lecture pure : le dashboard PDG n'expose aucune ecriture. Les deux bornes sont
        // obligatoires (le binding minimal-API repond 400 si l'une manque) ; l'ordre des
        // bornes et la taille de la fenetre sont controles par le service.
        pilotage.MapGet("/group-dashboard", async (
            DateOnly from,
            DateOnly to,
            IGroupDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetGroupDashboardAsync(from, to, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization("dashboard.read");

        return api;
    }
}
