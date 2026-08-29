using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Desktop.Api;

// Appels du module Encaissements et tresorerie (/api/v1/treasury/...).
// Fichier de classe partielle : SendAsync, ReadResponseAsync et
// EnsureAuthenticated sont definis dans RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    // ============================== Comptes bancaires ==============================

    public async Task<IReadOnlyCollection<BankAccountResponse>> GetBankAccountsAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeInactive ? "?includeInactive=true" : string.Empty;
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/treasury/bank-accounts{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<BankAccountResponse>>(response, cancellationToken);
    }

    public async Task<BankAccountResponse> CreateBankAccountAsync(
        string apiBaseUrl,
        CreateBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/treasury/bank-accounts", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BankAccountResponse>(response, cancellationToken);
    }

    public async Task<BankAccountResponse> UpdateBankAccountAsync(
        string apiBaseUrl,
        string code,
        UpdateBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/treasury/bank-accounts/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BankAccountResponse>(response, cancellationToken);
    }

    public async Task<BankAccountResponse> SetBankAccountActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/bank-accounts/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BankAccountResponse>(response, cancellationToken);
    }

    // ================================ Encaissements ================================

    public async Task<IReadOnlyCollection<CashReceiptResponse>> GetCashReceiptsAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        PaymentMethod? method,
        ReceiptStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildTreasuryReceiptQuery("/api/v1/treasury/receipts", from, to, hotelUnitCode, method, status),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<CashReceiptResponse>>(response, cancellationToken);
    }

    public async Task<CashReceiptSummaryResponse> GetCashReceiptSummaryAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReceiptStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildTreasuryReceiptQuery("/api/v1/treasury/receipts/summary", from, to, hotelUnitCode, method: null, status),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CashReceiptSummaryResponse>(response, cancellationToken);
    }

    public async Task<CashReceiptResponse> CreateCashReceiptAsync(
        string apiBaseUrl,
        CreateCashReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/treasury/receipts", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CashReceiptResponse>(response, cancellationToken);
    }

    public async Task<CashReceiptResponse> UpdateCashReceiptAsync(
        string apiBaseUrl,
        Guid id,
        UpdateCashReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/treasury/receipts/{id}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CashReceiptResponse>(response, cancellationToken);
    }

    public async Task<CashReceiptResponse> ConfirmCashReceiptAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/receipts/{id}/confirm", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CashReceiptResponse>(response, cancellationToken);
    }

    public async Task<CashReceiptResponse> CancelCashReceiptAsync(
        string apiBaseUrl,
        Guid id,
        CancelCashReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/receipts/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CashReceiptResponse>(response, cancellationToken);
    }

    // ============================== Ordres de paiement =============================

    public async Task<IReadOnlyCollection<PaymentOrderResponse>> GetPaymentOrdersAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? bankAccountCode,
        PaymentOrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildTreasuryPaymentOrderQuery("/api/v1/treasury/payment-orders", from, to, bankAccountCode, status),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PaymentOrderResponse>>(response, cancellationToken);
    }

    public async Task<PaymentOrderResponse> CreatePaymentOrderAsync(
        string apiBaseUrl,
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/treasury/payment-orders", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PaymentOrderResponse>(response, cancellationToken);
    }

    public async Task<PaymentOrderResponse> ApprovePaymentOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/payment-orders/{id}/approve", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PaymentOrderResponse>(response, cancellationToken);
    }

    public async Task<PaymentOrderResponse> PayPaymentOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/payment-orders/{id}/pay", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PaymentOrderResponse>(response, cancellationToken);
    }

    public async Task<PaymentOrderResponse> CancelPaymentOrderAsync(
        string apiBaseUrl,
        Guid id,
        CancelPaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/treasury/payment-orders/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PaymentOrderResponse>(response, cancellationToken);
    }

    // =================================== Requetes ==================================
    // Les enums sont serialises en chaine par l'API (JsonStringEnumConverter) :
    // la query utilise donc le nom du membre, comme l'attendent les endpoints.

    private static string BuildTreasuryReceiptQuery(
        string basePath,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        PaymentMethod? method,
        ReceiptStatus? status)
    {
        var query = new List<string>();

        AppendTreasuryDate(query, "from", from);
        AppendTreasuryDate(query, "to", to);
        AppendTreasuryText(query, "hotelUnitCode", hotelUnitCode);

        if (method.HasValue)
        {
            query.Add("method=" + Uri.EscapeDataString(method.Value.ToString()));
        }

        if (status.HasValue)
        {
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        return query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);
    }

    private static string BuildTreasuryPaymentOrderQuery(
        string basePath,
        DateOnly? from,
        DateOnly? to,
        string? bankAccountCode,
        PaymentOrderStatus? status)
    {
        var query = new List<string>();

        AppendTreasuryDate(query, "from", from);
        AppendTreasuryDate(query, "to", to);
        AppendTreasuryText(query, "bankAccountCode", bankAccountCode);

        if (status.HasValue)
        {
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        return query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);
    }

    private static void AppendTreasuryDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendTreasuryText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
