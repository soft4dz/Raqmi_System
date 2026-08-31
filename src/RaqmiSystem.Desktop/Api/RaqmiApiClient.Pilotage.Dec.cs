using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Desktop.Api;

// Module Pilotage - Cockpit DEC : appels du groupe /api/v1/pilotage/dec-cockpit.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules (dont le dashboard PDG) qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    /// <summary>
    /// Charge le cockpit quotidien de la DEC pour la date demandee : files de
    /// travail (recettes a valider, retard de cloture, recettes rejetees, ordres
    /// de paiement en attente), sante du jour par unite et indicateurs de charge.
    /// </summary>
    public async Task<DecCockpitResponse> GetDecCockpitAsync(
        string apiBaseUrl,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = "?date=" + Uri.EscapeDataString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"/api/v1/pilotage/dec-cockpit{query}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<DecCockpitResponse>(response, cancellationToken);
    }
}
