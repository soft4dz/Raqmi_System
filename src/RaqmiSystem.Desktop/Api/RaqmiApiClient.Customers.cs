using System.Net.Http;
using RaqmiSystem.Application.Billing;

namespace RaqmiSystem.Desktop.Api;

// Module Clients (fichier clients) : appels du groupe /api/v1/billing/customers.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string CustomersPath = "/api/v1/billing/customers";

    /// <summary>
    /// Liste le fichier clients. <paramref name="search"/> filtre cote serveur sur
    /// le code ou le nom ; les clients desactives ne remontent que si demande.
    /// </summary>
    public async Task<IReadOnlyCollection<CustomerResponse>> GetCustomersAsync(
        string apiBaseUrl,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCustomersQuery(search, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<CustomerResponse>>(response, cancellationToken);
    }

    public async Task<CustomerResponse> CreateCustomerAsync(
        string apiBaseUrl,
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, CustomersPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerResponse>(response, cancellationToken);
    }

    public async Task<CustomerResponse> UpdateCustomerAsync(
        string apiBaseUrl,
        string code,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{CustomersPath}/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerResponse>(response, cancellationToken);
    }

    public async Task<CustomerResponse> SetCustomerActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{CustomersPath}/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerResponse>(response, cancellationToken);
    }

    private static string BuildCustomersQuery(string? search, bool includeInactive)
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
            ? CustomersPath
            : CustomersPath + "?" + string.Join("&", query);
    }
}
