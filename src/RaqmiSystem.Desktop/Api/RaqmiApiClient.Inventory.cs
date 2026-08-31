using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Desktop.Api;

// Module Stocks & consommations (11) : appels du groupe /api/v1/inventory.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
//
// Rappel de doctrine visible jusque dans les signatures : il n'existe aucun appel
// qui "fixe" un stock. On ajoute un mouvement (CreateStockMovementAsync), on
// transfere (CreateStockTransferAsync, deux moities liees en une transaction), ou
// on valide un inventaire (ValidateInventoryCountAsync) qui genere lui-meme ses
// mouvements d'ajustement. Le stock lu est toujours la somme du registre, calculee
// par le serveur.
public sealed partial class RaqmiApiClient
{
    private const string InventoryPath = "/api/v1/inventory";

    // ------------------------------ Magasins -------------------------------

    public async Task<IReadOnlyCollection<WarehouseResponse>> GetWarehousesAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = $"{InventoryPath}/warehouses" + (includeInactive ? "?includeInactive=true" : string.Empty);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<WarehouseResponse>>(response, cancellationToken);
    }

    public async Task<WarehouseResponse> CreateWarehouseAsync(
        string apiBaseUrl,
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/warehouses", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<WarehouseResponse>(response, cancellationToken);
    }

    public async Task<WarehouseResponse> UpdateWarehouseAsync(
        string apiBaseUrl,
        string code,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{InventoryPath}/warehouses/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<WarehouseResponse>(response, cancellationToken);
    }

    public async Task<WarehouseResponse> SetWarehouseActiveAsync(
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
            $"{InventoryPath}/warehouses/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<WarehouseResponse>(response, cancellationToken);
    }

    // ------------------------------- Articles ------------------------------

    public async Task<IReadOnlyCollection<StockItemResponse>> GetStockItemsAsync(
        string apiBaseUrl,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = $"{InventoryPath}/items" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<StockItemResponse>>(response, cancellationToken);
    }

    public async Task<StockItemResponse> CreateStockItemAsync(
        string apiBaseUrl,
        CreateStockItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/items", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<StockItemResponse>(response, cancellationToken);
    }

    public async Task<StockItemResponse> UpdateStockItemAsync(
        string apiBaseUrl,
        string code,
        UpdateStockItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{InventoryPath}/items/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<StockItemResponse>(response, cancellationToken);
    }

    public async Task<StockItemResponse> SetStockItemActiveAsync(
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
            $"{InventoryPath}/items/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<StockItemResponse>(response, cancellationToken);
    }

    // ------------------------------ Mouvements -----------------------------

    public async Task<IReadOnlyCollection<StockMovementResponse>> GetStockMovementsAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? warehouseCode,
        string? itemCode,
        StockMovementKind? kind,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendInventoryDate(query, "from", from);
        AppendInventoryDate(query, "to", to);

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            query.Add("warehouseCode=" + Uri.EscapeDataString(warehouseCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            query.Add("itemCode=" + Uri.EscapeDataString(itemCode.Trim()));
        }

        if (kind.HasValue)
        {
            query.Add("kind=" + Uri.EscapeDataString(kind.Value.ToString()));
        }

        var path = $"{InventoryPath}/movements" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<StockMovementResponse>>(response, cancellationToken);
    }

    public async Task<StockMovementResponse> CreateStockMovementAsync(
        string apiBaseUrl,
        CreateStockMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/movements", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<StockMovementResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Transfert inter-magasins : un seul appel, deux mouvements lies crees
    /// atomiquement cote serveur. Il n'existe deliberement pas d'appel qui
    /// n'enregistrerait qu'une moitie.
    /// </summary>
    public async Task<StockTransferResponse> CreateStockTransferAsync(
        string apiBaseUrl,
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/transfers", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<StockTransferResponse>(response, cancellationToken);
    }

    // --------------------------- Stock et alertes --------------------------

    public async Task<WarehouseStockResponse> GetWarehouseStockAsync(
        string apiBaseUrl,
        string warehouseCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{InventoryPath}/warehouses/{Uri.EscapeDataString(warehouseCode)}/stock",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<WarehouseStockResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LowStockRow>> GetLowStockAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{InventoryPath}/low-stock", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<LowStockRow>>(response, cancellationToken);
    }

    // ----------------------------- Inventaires -----------------------------

    public async Task<IReadOnlyCollection<InventoryCountResponse>> GetInventoryCountsAsync(
        string apiBaseUrl,
        string? warehouseCode,
        InventoryCountStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            query.Add("warehouseCode=" + Uri.EscapeDataString(warehouseCode.Trim()));
        }

        if (status.HasValue)
        {
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        var path = $"{InventoryPath}/counts" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<InventoryCountResponse>>(response, cancellationToken);
    }

    public async Task<InventoryCountResponse> GetInventoryCountAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{InventoryPath}/counts/{id}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InventoryCountResponse>(response, cancellationToken);
    }

    public async Task<InventoryCountResponse> CreateInventoryCountAsync(
        string apiBaseUrl,
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/counts", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InventoryCountResponse>(response, cancellationToken);
    }

    public async Task<InventoryCountResponse> ReplaceInventoryCountLinesAsync(
        string apiBaseUrl,
        Guid id,
        ReplaceInventoryCountLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{InventoryPath}/counts/{id}/lines", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InventoryCountResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Valide un inventaire : le serveur genere les mouvements d'ajustement puis
    /// fige l'inventaire pour toujours. La reponse dit combien d'ajustements ont
    /// ete generes - chiffre affiche tel quel, jamais recalcule a l'ecran.
    /// </summary>
    public async Task<InventoryCountValidationResponse> ValidateInventoryCountAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{InventoryPath}/counts/{id}/validate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InventoryCountValidationResponse>(response, cancellationToken);
    }

    private static void AppendInventoryDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }
}
