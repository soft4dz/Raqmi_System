using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;

namespace RaqmiSystem.Application.Fiscalite;

/// <summary>
/// SIFEC e-invoicing connector. Sandbox mode simulates a systematic DGI acceptance for
/// certification/testing; production mode has no real DGI integration in this codebase and every
/// call MUST fail explicitly - see the implementation's remarks. Never treat a sandbox acceptance
/// as if the invoice had actually reached the DGI.
/// </summary>
public interface ISifecService
{
    Task<SifecHubSummaryResponse> GetHubSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SifecTransmissionResponse>> ListTransmissionsAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<SifecTransmissionResponse>> PrepareAsync(
        Guid invoiceId, OperationContext context, CancellationToken cancellationToken);

    Task<ApplicationResult<SifecTransmissionResponse>> SubmitAsync(
        Guid invoiceId, OperationContext context, CancellationToken cancellationToken);

    /// <summary>Prepares (if needed) then submits every issued invoice not yet Accepte.</summary>
    Task<ApplicationResult<IReadOnlyCollection<SifecTransmissionResponse>>> SubmitBatchAsync(
        OperationContext context, CancellationToken cancellationToken);

    Task<SifecConfigResponse> GetConfigAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<SifecConfigResponse>> SaveConfigAsync(
        SaveSifecConfigRequest request, OperationContext context, CancellationToken cancellationToken);

    /// <summary>
    /// In Sandbox mode, always reports success. In Production mode, always reports failure with an
    /// explicit "not implemented" message - see the implementation.
    /// </summary>
    Task<ApplicationResult<SifecConfigResponse>> TestConnectionAsync(OperationContext context, CancellationToken cancellationToken);
}

public sealed record SifecHubSummaryResponse(
    int EnAttente, int Soumis, int Acceptes, int Rejetes, int Erreurs, SifecMode Mode);

public sealed record SifecTransmissionResponse(
    Guid Id, Guid InvoiceId, SifecTransmissionStatus Status, string? Uid, SifecMode Mode,
    DateTimeOffset? SubmittedAt, string? ResponseMessage);

public sealed record SifecConfigResponse(
    SifecMode Mode, string? ApiUrl, string? ApiKeyReference, string? DeclarantNif, bool IsActive,
    DateTimeOffset? LastConnectionTestAt, bool? LastConnectionTestSucceeded);

public sealed record SaveSifecConfigRequest(
    SifecMode Mode, string? ApiUrl, string? ApiKeyReference, string? DeclarantNif, bool IsActive);
