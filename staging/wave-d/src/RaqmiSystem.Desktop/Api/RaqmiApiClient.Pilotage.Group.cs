using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Desktop.Api;

// Module Pilotage (dashboard PDG) : appel du groupe /api/v1/pilotage/group-dashboard.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec les
// autres modules qui alimentent le meme client API (dont le Cockpit DEC, mene en
// parallele dans son propre fichier partiel).
public sealed partial class RaqmiApiClient
{
    private const string PilotageGroupDashboardPath = "/api/v1/pilotage/group-dashboard";

    /// <summary>
    /// Charge le tableau de bord groupe sur la periode [from, to] : KPI groupe, comparaison
    /// N/N-1, tableau des unites classees par chiffre d'affaires et alertes de direction.
    /// Lecture pure - aucune ecriture derriere cet appel.
    /// </summary>
    public async Task<GroupDashboardResponse> GetGroupDashboardAsync(
        string apiBaseUrl,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = PilotageGroupDashboardPath
            + "?from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            + "&to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            path,
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<GroupDashboardResponse>(response, cancellationToken);
    }
}
