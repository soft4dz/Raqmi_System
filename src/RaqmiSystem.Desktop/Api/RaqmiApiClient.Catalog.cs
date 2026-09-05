using System.Net.Http;
using RaqmiSystem.Application.Catalog;

namespace RaqmiSystem.Desktop.Api;

// Catalogue des articles vendables : appels du groupe /api/v1/catalog/articles.
// L'ecran Facturation s'en sert pour proposer un article sur une ligne de
// facture ; le prix et la TVA proposes viennent de la reponse du serveur, le
// poste ne calcule rien.
public sealed partial class RaqmiApiClient
{
    private const string CatalogPath = "/api/v1/catalog/articles";

    public async Task<IReadOnlyCollection<ArticleResponse>> GetArticlesAsync(
        string apiBaseUrl,
        string? search,
        string? family,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(family))
        {
            query.Add("family=" + Uri.EscapeDataString(family.Trim()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = query.Count == 0 ? CatalogPath : CatalogPath + "?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ArticleResponse>>(response, cancellationToken);
    }
}
