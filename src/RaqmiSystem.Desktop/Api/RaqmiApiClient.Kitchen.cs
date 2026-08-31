using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Desktop.Api;

// Module Cuisine, production & qualite (11.5) : appels des groupes
// /api/v1/kitchen/recipes, /api/v1/kitchen/checkpoints et /api/v1/kitchen/readings.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string KitchenRecipesPath = "/api/v1/kitchen/recipes";

    private const string KitchenCheckpointsPath = "/api/v1/kitchen/checkpoints";

    private const string KitchenReadingsPath = "/api/v1/kitchen/readings";

    // ============================== Fiches techniques ==============================

    /// <summary>
    /// Liste les fiches techniques. <paramref name="search"/> filtre cote serveur sur
    /// le code ou le nom ; les fiches desactivees ne remontent que si demande.
    /// </summary>
    public async Task<IReadOnlyCollection<RecipeResponse>> GetRecipesAsync(
        string apiBaseUrl,
        string? search,
        RecipeCategory? category,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildRecipesQuery(search, category, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RecipeResponse>>(response, cancellationToken);
    }

    public async Task<RecipeResponse> GetRecipeAsync(
        string apiBaseUrl,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{KitchenRecipesPath}/{Uri.EscapeDataString(code)}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RecipeResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Cout matiere de la fiche : quantite x cout moyen pondere COURANT de chaque
    /// ingredient, total et cout par portion. Le serveur signale les ingredients sans
    /// cout connu (hasCost = false) et les exclut du total ; le client ne recalcule
    /// jamais ces montants.
    /// </summary>
    public async Task<RecipeCostResponse> GetRecipeCostAsync(
        string apiBaseUrl,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{KitchenRecipesPath}/{Uri.EscapeDataString(code)}/cost",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RecipeCostResponse>(response, cancellationToken);
    }

    public async Task<RecipeResponse> CreateRecipeAsync(
        string apiBaseUrl,
        CreateRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, KitchenRecipesPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RecipeResponse>(response, cancellationToken);
    }

    public async Task<RecipeResponse> UpdateRecipeAsync(
        string apiBaseUrl,
        string code,
        UpdateRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{KitchenRecipesPath}/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RecipeResponse>(response, cancellationToken);
    }

    public async Task<RecipeResponse> SetRecipeActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{KitchenRecipesPath}/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RecipeResponse>(response, cancellationToken);
    }

    // ============================== Points de controle ==============================

    public async Task<IReadOnlyCollection<TemperatureCheckpointResponse>> GetTemperatureCheckpointsAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = includeInactive
            ? KitchenCheckpointsPath + "?includeInactive=true"
            : KitchenCheckpointsPath;

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<TemperatureCheckpointResponse>>(response, cancellationToken);
    }

    public async Task<TemperatureCheckpointResponse> CreateTemperatureCheckpointAsync(
        string apiBaseUrl,
        CreateTemperatureCheckpointRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, KitchenCheckpointsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<TemperatureCheckpointResponse>(response, cancellationToken);
    }

    public async Task<TemperatureCheckpointResponse> UpdateTemperatureCheckpointAsync(
        string apiBaseUrl,
        string code,
        UpdateTemperatureCheckpointRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{KitchenCheckpointsPath}/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<TemperatureCheckpointResponse>(response, cancellationToken);
    }

    public async Task<TemperatureCheckpointResponse> SetTemperatureCheckpointActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{KitchenCheckpointsPath}/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<TemperatureCheckpointResponse>(response, cancellationToken);
    }

    // ============================== Releves HACCP ==============================

    /// <summary>
    /// Historique des releves de temperature d'une periode. Les seuils portes par
    /// chaque releve sont ceux FIGES au moment de la mesure, pas la plage courante du
    /// point de controle.
    /// </summary>
    public async Task<IReadOnlyCollection<TemperatureReadingResponse>> GetTemperatureReadingsAsync(
        string apiBaseUrl,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? checkpointCode,
        bool nonCompliantOnly,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildReadingsQuery(from, to, checkpointCode, nonCompliantOnly),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<TemperatureReadingResponse>>(response, cancellationToken);
    }

    public async Task<TemperatureReadingResponse> CreateTemperatureReadingAsync(
        string apiBaseUrl,
        CreateTemperatureReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, KitchenReadingsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<TemperatureReadingResponse>(response, cancellationToken);
    }

    // ============================== Construction des requetes ==============================

    private static string BuildRecipesQuery(string? search, RecipeCategory? category, bool includeInactive)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        }

        if (category.HasValue)
        {
            query.Add("category=" + Uri.EscapeDataString(category.Value.ToString()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        return query.Count == 0
            ? KitchenRecipesPath
            : KitchenRecipesPath + "?" + string.Join("&", query);
    }

    private static string BuildReadingsQuery(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? checkpointCode,
        bool nonCompliantOnly)
    {
        var query = new List<string>();

        // Format aller-retour "O" : l'instant part avec son decalage, comme le journal
        // d'audit - le serveur compare des instants, pas des dates locales.
        if (from.HasValue)
        {
            query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (to.HasValue)
        {
            query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(checkpointCode))
        {
            query.Add("checkpointCode=" + Uri.EscapeDataString(checkpointCode.Trim()));
        }

        if (nonCompliantOnly)
        {
            query.Add("nonCompliantOnly=true");
        }

        return query.Count == 0
            ? KitchenReadingsPath
            : KitchenReadingsPath + "?" + string.Join("&", query);
    }
}
