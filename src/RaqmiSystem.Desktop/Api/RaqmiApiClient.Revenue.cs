using System.Net.Http;
using RaqmiSystem.Application.Revenue;

namespace RaqmiSystem.Desktop.Api;

// Partie "categories de recettes" du client API : la section /api/v1/revenue/categories
// de RevenueEndpoints. Les appels de saisie des recettes (/api/v1/revenue/daily) vivent
// dans le fichier principal du client ; seul l'appel dont les ecrans de saisie ont besoin
// pour se construire figure ici. Le paramétrage des catégories (création, modification,
// suppression) n'a pas d'écran : il n'est pas exposé tant qu'aucune vue ne le déclenche.
public sealed partial class RaqmiApiClient
{
    /// <summary>
    /// Les catégories de recettes qui s'appliquent à une unité (le jeu dédié à son secteur, sinon
    /// le jeu générique), actives, dans l'ordre d'affichage du serveur. Sans unité : toutes les
    /// catégories actives. C'est cette liste, et elle seule, qui pilote les champs de saisie et
    /// les colonnes des écrans : aucune catégorie n'est connue du poste.
    /// </summary>
    public async Task<IReadOnlyCollection<RevenueCategoryResponse>> GetRevenueCategoriesAsync(
        string apiBaseUrl,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = string.IsNullOrWhiteSpace(hotelUnitCode)
            ? string.Empty
            : "?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode.Trim());

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"/api/v1/revenue/categories{query}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RevenueCategoryResponse>>(response, cancellationToken);
    }
}
