using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Fiscalite;

namespace RaqmiSystem.Desktop.Api;

// Appels du module Fiscalite DGI & SIFEC (/api/v1/fiscalite/...) : registres TVA,
// declaration mensuelle, G50, retenue a la source, liasse fiscale et connecteur SIFEC.
//
// Fichier de classe partielle : SendAsync, ReadResponseAsync et EnsureAuthenticated
// sont definis dans RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    public async Task<IReadOnlyCollection<VatSalesRegisterEntryResponse>> GetVatSalesRegisterAsync(
        string apiBaseUrl, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, PeriodPath("/api/v1/fiscalite/registre-tva-ventes", year, month), null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<VatSalesRegisterEntryResponse>>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<VatPurchaseRegisterEntryResponse>> GetVatPurchasesRegisterAsync(
        string apiBaseUrl, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, PeriodPath("/api/v1/fiscalite/registre-tva-achats", year, month), null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<VatPurchaseRegisterEntryResponse>>(response, cancellationToken);
    }

    public async Task<VatPurchaseRegisterEntryResponse> CreateVatPurchaseEntryAsync(
        string apiBaseUrl, CreateVatPurchaseEntryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/fiscalite/registre-tva-achats", request, true, cancellationToken);
        return await ReadResponseAsync<VatPurchaseRegisterEntryResponse>(response, cancellationToken);
    }

    public async Task<int> ImportApprovedPurchaseOrdersAsync(
        string apiBaseUrl, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, PeriodPath("/api/v1/fiscalite/registre-tva-achats/import", year, month), null, true, cancellationToken);
        return await ReadResponseAsync<int>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<VatDeclarationResponse>> GetVatDeclarationHistoryAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/declarations", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<VatDeclarationResponse>>(response, cancellationToken);
    }

    public async Task<VatDeclarationResponse> CalculateVatDeclarationAsync(
        string apiBaseUrl, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, PeriodPath("/api/v1/fiscalite/declarations/calculate", year, month), null, true, cancellationToken);
        return await ReadResponseAsync<VatDeclarationResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TeleDeclarationResponse>> GetTeleDeclarationsAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/teledeclarations", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<TeleDeclarationResponse>>(response, cancellationToken);
    }

    public async Task<G50ExportResult> ExportG50Async(
        string apiBaseUrl, int year, int month, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, PeriodPath("/api/v1/fiscalite/teledeclarations/export-g50", year, month), null, true, cancellationToken);
        return await ReadResponseAsync<G50ExportResult>(response, cancellationToken);
    }

    public async Task<TeleDeclarationResponse> MarkTeleDeclarationDeclaredAsync(
        string apiBaseUrl, Guid teleDeclarationId, string dgiReference, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(
            apiBaseUrl, HttpMethod.Post, $"/api/v1/fiscalite/teledeclarations/{teleDeclarationId}/marquer-declaree",
            new MarkDeclaredPayload(dgiReference), true, cancellationToken);
        return await ReadResponseAsync<TeleDeclarationResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<WithholdingTaxEntryResponse>> GetWithholdingTaxEntriesAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/retenues-source", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<WithholdingTaxEntryResponse>>(response, cancellationToken);
    }

    public async Task<WithholdingTaxEntryResponse> CreateWithholdingTaxEntryAsync(
        string apiBaseUrl, CreateWithholdingTaxEntryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/fiscalite/retenues-source", request, true, cancellationToken);
        return await ReadResponseAsync<WithholdingTaxEntryResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FiscalReturnResponse>> GetFiscalReturnsAsync(
        string apiBaseUrl, int year, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/fiscalite/liasse?year={year.ToString(CultureInfo.InvariantCulture)}", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<FiscalReturnResponse>>(response, cancellationToken);
    }

    public async Task<FiscalReturnResponse> GenerateSimpleFiscalReturnAsync(
        string apiBaseUrl, int year, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/fiscalite/liasse/generer?year={year.ToString(CultureInfo.InvariantCulture)}", null, true, cancellationToken);
        return await ReadResponseAsync<FiscalReturnResponse>(response, cancellationToken);
    }

    public async Task<FiscalReturnResponse> GenerateAdvancedFiscalReturnAsync(
        string apiBaseUrl, int year, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/fiscalite/liasse/generer-avancee?year={year.ToString(CultureInfo.InvariantCulture)}", null, true, cancellationToken);
        return await ReadResponseAsync<FiscalReturnResponse>(response, cancellationToken);
    }

    public async Task<SifecHubSummaryResponse> GetSifecHubSummaryAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/sifec/hub", null, true, cancellationToken);
        return await ReadResponseAsync<SifecHubSummaryResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SifecTransmissionResponse>> GetSifecTransmissionsAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/sifec/factures", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<SifecTransmissionResponse>>(response, cancellationToken);
    }

    public async Task<SifecTransmissionResponse> SendSifecInvoiceAsync(
        string apiBaseUrl, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/fiscalite/sifec/factures/{invoiceId}/envoyer", null, true, cancellationToken);
        return await ReadResponseAsync<SifecTransmissionResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SifecTransmissionResponse>> SubmitSifecBatchAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/fiscalite/sifec/factures/transmettre-lot", null, true, cancellationToken);
        return await ReadResponseAsync<IReadOnlyCollection<SifecTransmissionResponse>>(response, cancellationToken);
    }

    public async Task<SifecConfigResponse> GetSifecConfigAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/fiscalite/sifec/config", null, true, cancellationToken);
        return await ReadResponseAsync<SifecConfigResponse>(response, cancellationToken);
    }

    public async Task<SifecConfigResponse> SaveSifecConfigAsync(
        string apiBaseUrl, SaveSifecConfigRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, "/api/v1/fiscalite/sifec/config", request, true, cancellationToken);
        return await ReadResponseAsync<SifecConfigResponse>(response, cancellationToken);
    }

    public async Task<SifecConfigResponse> TestSifecConnectionAsync(
        string apiBaseUrl, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/fiscalite/sifec/config/test-connexion", null, true, cancellationToken);
        return await ReadResponseAsync<SifecConfigResponse>(response, cancellationToken);
    }

    private static string PeriodPath(string basePath, int year, int month)
    {
        return $"{basePath}?year={year.ToString(CultureInfo.InvariantCulture)}&month={month.ToString(CultureInfo.InvariantCulture)}";
    }

    private sealed record MarkDeclaredPayload(string DgiReference);
}
