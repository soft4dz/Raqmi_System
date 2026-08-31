using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Purchasing;

namespace RaqmiSystem.Desktop.Api;

// Module Achats & approvisionnements : appels des groupes /api/v1/purchasing/suppliers
// et /api/v1/purchasing/orders. Fichier de classe partielle, pour que ce chantier
// n'entre pas en conflit avec les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string SuppliersPath = "/api/v1/purchasing/suppliers";

    private const string PurchaseOrdersPath = "/api/v1/purchasing/orders";

    /// <summary>
    /// Liste le referentiel fournisseurs. <paramref name="search"/> filtre cote serveur
    /// sur le code ou le nom ; les fournisseurs desactives ne remontent que si demande.
    /// </summary>
    public async Task<IReadOnlyCollection<SupplierResponse>> GetSuppliersAsync(
        string apiBaseUrl,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildSuppliersQuery(search, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<SupplierResponse>>(response, cancellationToken);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(
        string apiBaseUrl,
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, SuppliersPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<SupplierResponse>(response, cancellationToken);
    }

    public async Task<SupplierResponse> UpdateSupplierAsync(
        string apiBaseUrl,
        string code,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{SuppliersPath}/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<SupplierResponse>(response, cancellationToken);
    }

    public async Task<SupplierResponse> SetSupplierActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SuppliersPath}/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<SupplierResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Liste les bons de commande. Le statut est transmis sous sa forme du domaine
    /// (Draft, Approved, PartiallyReceived, Received, Cancelled) : seul l'affichage est
    /// traduit en francais, jamais la valeur envoyee au serveur.
    /// </summary>
    public async Task<IReadOnlyCollection<PurchaseOrderResponse>> GetPurchaseOrdersAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? supplierCode,
        string? warehouseCode,
        string? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildPurchaseOrderQuery(from, to, supplierCode, warehouseCode, status),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PurchaseOrderResponse>>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> GetPurchaseOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{PurchaseOrdersPath}/{id}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> CreatePurchaseOrderAsync(
        string apiBaseUrl,
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, PurchaseOrdersPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> UpdatePurchaseOrderLinesAsync(
        string apiBaseUrl,
        Guid id,
        UpdatePurchaseOrderLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{PurchaseOrdersPath}/{id}/lines", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> ApprovePurchaseOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{PurchaseOrdersPath}/{id}/approve", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> ReceivePurchaseOrderAsync(
        string apiBaseUrl,
        Guid id,
        ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{PurchaseOrdersPath}/{id}/receive", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    public async Task<PurchaseOrderResponse> CancelPurchaseOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{PurchaseOrdersPath}/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PurchaseOrderResponse>(response, cancellationToken);
    }

    private static string BuildSuppliersQuery(string? search, bool includeInactive)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        return query.Count == 0
            ? SuppliersPath
            : SuppliersPath + "?" + string.Join("&", query);
    }

    private static string BuildPurchaseOrderQuery(
        DateOnly? from,
        DateOnly? to,
        string? supplierCode,
        string? warehouseCode,
        string? status)
    {
        var query = new List<string>();

        if (from.HasValue)
        {
            query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        if (to.HasValue)
        {
            query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(supplierCode))
        {
            query.Add("supplierCode=" + Uri.EscapeDataString(supplierCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            query.Add("warehouseCode=" + Uri.EscapeDataString(warehouseCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add("status=" + Uri.EscapeDataString(status.Trim()));
        }

        return query.Count == 0
            ? PurchaseOrdersPath
            : PurchaseOrdersPath + "?" + string.Join("&", query);
    }
}
