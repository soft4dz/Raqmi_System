using System.Globalization;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Pilotage : les deux ecrans de direction, reunis derriere UN SEUL groupe de routes
/// "/pilotage" et UNE SEULE methode d'extension - deux MapGroup("/pilotage") concurrents
/// produiraient deux groupes distincts sur le meme prefixe, chacun avec ses propres tags et
/// filtres, pour un unique module fonctionnel.
///
/// - GET /pilotage/group-dashboard : Dashboard PDG (24.2), vision groupe sur une periode avec
///   comparaison N/N-1.
/// - GET /pilotage/dec-cockpit : Cockpit DEC (24.4), files de travail du jour et sante des
///   unites.
///
/// LECTURE PURE : aucune de ces routes n'ecrit quoi que ce soit, et les deux modules ne
/// possedent aucune table - ils agregent les donnees des modules existants. La permission est
/// donc la cle DEJA existante <see cref="PermissionCatalog.DashboardRead"/> (semee pour
/// direction, exploitation.control, unit.manager et reader) : aucune cle nouvelle n'est creee
/// pour ce module.
/// </summary>
internal static class PilotageEndpoints
{
    public static RouteGroupBuilder MapPilotageEndpoints(this RouteGroupBuilder api)
    {
        var pilotage = api.MapGroup("/pilotage")
            .WithTags("Pilotage");

        // Dashboard PDG. Les deux bornes sont obligatoires (le binding minimal-API repond 400
        // si l'une manque) ; l'ordre des bornes et la taille de la fenetre sont controles par
        // le service, qui repond alors en validation.
        pilotage.MapGet("/group-dashboard", async (
            DateOnly from,
            DateOnly to,
            IGroupDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetGroupDashboardAsync(from, to, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        // Cockpit DEC. La date est optionnelle et vaut alors le jour metier UTC - convention
        // du depot pour toute decision fondee sur UtcNow. Une date illisible est un 400
        // explicite plutot qu'un repli silencieux sur aujourd'hui, qui ferait lire au
        // controleur un cockpit qu'il n'a pas demande.
        pilotage.MapGet("/dec-cockpit", async (
            string? date,
            IDecCockpitService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseCockpitDate(date, out var cockpitDate, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetCockpitAsync(cockpitDate, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.DashboardRead);

        return api;
    }

    private static bool TryParseCockpitDate(string? date, out DateOnly cockpitDate, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(date))
        {
            cockpitDate = DateOnly.FromDateTime(DateTime.UtcNow);
            return true;
        }

        if (DateOnly.TryParse(date.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out cockpitDate))
        {
            return true;
        }

        error = "The date must be a valid calendar date (yyyy-MM-dd).";
        return false;
    }
}
