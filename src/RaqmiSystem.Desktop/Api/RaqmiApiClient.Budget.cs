using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Budgeting;

namespace RaqmiSystem.Desktop.Api;

// Partie "Budget & previsions" du client API : la section /api/v1/budget de
// BudgetEndpoints. Les unites hotelieres qui alimentent la liste deroulante de
// cet ecran passent par GetHotelUnitsAsync (fichier principal du client) : meme
// route, meme reponse, une seule implementation.
//
// Seuls les appels reellement utilises par l'ecran Budget figurent ici. Les
// operations de l'API qui n'y ont pas d'equivalent (modification du libelle,
// reglage d'une ligne isolee, suppression d'une ligne, cloture de l'exercice)
// ne sont pas exposees tant qu'aucune vue ne les declenche.
public sealed partial class RaqmiApiClient
{
    public async Task<IReadOnlyCollection<BudgetPlanResponse>> GetBudgetPlansAsync(
        string apiBaseUrl,
        int? year,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildBudgetPlanQuery(year, hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<BudgetPlanResponse>>(response, cancellationToken);
    }

    public async Task<BudgetPlanResponse> CreateBudgetPlanAsync(
        string apiBaseUrl,
        CreateBudgetPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/budget/plans", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BudgetPlanResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Remplace d'un seul coup toute la grille des objectifs du plan. Le serveur
    /// refuse deux objectifs pour un meme couple (mois, categorie) : la vue
    /// n'envoie donc qu'une ligne par cellule.
    /// </summary>
    public async Task<BudgetPlanResponse> ReplaceBudgetPlanLinesAsync(
        string apiBaseUrl,
        Guid id,
        ReplaceBudgetLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/budget/plans/{id}/lines", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BudgetPlanResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Approuve le plan : acte engageant qui fige le budget (permission
    /// budget.approve, distincte de budget.write cote serveur).
    /// </summary>
    public async Task<BudgetPlanResponse> ApproveBudgetPlanAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/budget/plans/{id}/approve", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BudgetPlanResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Confronte le budget de l'exercice au realise. L'annee et le code d'unite
    /// sont exiges par l'API : sans plan pour ce couple, elle repond 404 plutot
    /// que d'inventer une grille d'objectifs a zero.
    /// </summary>
    public async Task<BudgetVarianceResponse> GetBudgetVarianceAsync(
        string apiBaseUrl,
        int year,
        string hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = "?year=" + year.ToString(CultureInfo.InvariantCulture)
            + "&hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode.Trim());

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/budget/variance{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BudgetVarianceResponse>(response, cancellationToken);
    }

    private static string BuildBudgetPlanQuery(int? year, string? hotelUnitCode)
    {
        var query = new List<string>();

        if (year.HasValue)
        {
            query.Add("year=" + year.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            query.Add("hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode.Trim()));
        }

        return query.Count == 0
            ? "/api/v1/budget/plans"
            : "/api/v1/budget/plans?" + string.Join("&", query);
    }
}
