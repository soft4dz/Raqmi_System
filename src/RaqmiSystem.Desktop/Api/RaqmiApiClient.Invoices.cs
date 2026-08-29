using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Billing;

namespace RaqmiSystem.Desktop.Api;

// Partie "Facturation" du client API : la section /api/v1/billing/invoices de
// BillingEndpoints. La lecture du fichier clients qui alimente les listes
// deroulantes de cet ecran passe par GetCustomersAsync (fichier partiel du
// module Clients) : meme route, meme reponse, une seule implementation.
public sealed partial class RaqmiApiClient
{
    public async Task<IReadOnlyCollection<InvoiceResponse>> GetInvoicesAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        string? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildInvoiceQuery(from, to, customerCode, hotelUnitCode, status),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<InvoiceResponse>>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> GetInvoiceAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/billing/invoices/{id}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(
        string apiBaseUrl,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/billing/invoices", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> UpdateInvoiceLinesAsync(
        string apiBaseUrl,
        Guid id,
        UpdateInvoiceLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/billing/invoices/{id}/lines", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> IssueInvoiceAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/billing/invoices/{id}/issue", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> MarkInvoicePaidAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/billing/invoices/{id}/pay", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    public async Task<InvoiceResponse> CancelInvoiceAsync(
        string apiBaseUrl,
        Guid id,
        CancelInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/billing/invoices/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<InvoiceResponse>(response, cancellationToken);
    }

    private static string BuildInvoiceQuery(
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
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

        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            query.Add("customerCode=" + Uri.EscapeDataString(customerCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            query.Add("hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add("status=" + Uri.EscapeDataString(status.Trim()));
        }

        return query.Count == 0
            ? "/api/v1/billing/invoices"
            : "/api/v1/billing/invoices?" + string.Join("&", query);
    }
}
