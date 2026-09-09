using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Fiscalite;

/// <summary>
/// SIFEC e-invoicing connector. Sandbox mode ALWAYS simulates a DGI acceptance (certification and
/// testing, exactly like the legacy product); production mode has NO real DGI integration in this
/// codebase and every submission MUST fail explicitly with <see cref="ProductionNotImplementedMessage"/>
/// - never report a simulated success as if the invoice had actually reached the DGI.
/// </summary>
public sealed class SifecService(RaqmiDbContext dbContext, IAuditLogWriter auditLogWriter) : ISifecService
{
    public const string ProductionNotImplementedMessage =
        "Integration DGI en mode production non implementee dans cette version - utilisez le mode sandbox pour les tests et la certification.";

    private const string SifecEntity = "fiscalite.sifec";
    private const string ConfigEntity = "fiscalite.sifec_config";

    public async Task<SifecHubSummaryResponse> GetHubSummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await dbContext.Set<SifecTransmission>()
            .AsNoTracking()
            .GroupBy(transmission => transmission.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var config = await LoadOrDefaultConfigAsync(cancellationToken);

        return new SifecHubSummaryResponse(
            EnAttente: counts.GetValueOrDefault(SifecTransmissionStatus.Prepare),
            Soumis: counts.GetValueOrDefault(SifecTransmissionStatus.Soumis),
            Acceptes: counts.GetValueOrDefault(SifecTransmissionStatus.Accepte),
            Rejetes: counts.GetValueOrDefault(SifecTransmissionStatus.Rejete),
            Erreurs: counts.GetValueOrDefault(SifecTransmissionStatus.Erreur),
            Mode: config.Mode);
    }

    public async Task<IReadOnlyCollection<SifecTransmissionResponse>> ListTransmissionsAsync(CancellationToken cancellationToken)
    {
        var transmissions = await dbContext.Set<SifecTransmission>()
            .AsNoTracking()
            .OrderByDescending(transmission => transmission.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return transmissions.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<SifecTransmissionResponse>> PrepareAsync(
        Guid invoiceId, OperationContext context, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<SifecTransmissionResponse>.NotFound("Invoice was not found.");
        }

        if (invoice.Status != InvoiceStatus.Issued && invoice.Status != InvoiceStatus.Paid)
        {
            return ApplicationResult<SifecTransmissionResponse>.Validation(
                "Only an issued invoice can be prepared for SIFEC transmission.");
        }

        var alreadyAccepted = await dbContext.Set<SifecTransmission>()
            .AnyAsync(transmission => transmission.InvoiceId == invoiceId && transmission.Status == SifecTransmissionStatus.Accepte, cancellationToken);

        if (alreadyAccepted)
        {
            return ApplicationResult<SifecTransmissionResponse>.Conflict("This invoice has already been accepted by the DGI.");
        }

        var config = await LoadOrDefaultConfigAsync(cancellationToken);
        var (payloadHash, qrPayload) = BuildPayload(invoice, config);

        var transmission = new SifecTransmission(invoiceId, payloadHash, qrPayload, config.Mode);
        transmission.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<SifecTransmission>().Add(transmission);

        await WriteAuditAsync(
            "fiscalite.sifec.prepared", SifecEntity, transmission.Id, context,
            new { InvoiceId = invoiceId, Mode = config.Mode.ToString() }, cancellationToken);

        return ApplicationResult<SifecTransmissionResponse>.Success(Map(transmission));
    }

    public async Task<ApplicationResult<SifecTransmissionResponse>> SubmitAsync(
        Guid invoiceId, OperationContext context, CancellationToken cancellationToken)
    {
        var transmission = await dbContext.Set<SifecTransmission>()
            .Where(current => current.InvoiceId == invoiceId)
            .OrderByDescending(current => current.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (transmission is null || transmission.Status == SifecTransmissionStatus.Accepte)
        {
            var prepared = await PrepareAsync(invoiceId, context, cancellationToken);

            if (!prepared.Succeeded || prepared.Value is null)
            {
                return prepared;
            }

            transmission = await dbContext.Set<SifecTransmission>().SingleAsync(current => current.Id == prepared.Value.Id, cancellationToken);
        }

        await SubmitTransmissionAsync(transmission, context, cancellationToken);

        return ApplicationResult<SifecTransmissionResponse>.Success(Map(transmission));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<SifecTransmissionResponse>>> SubmitBatchAsync(
        OperationContext context, CancellationToken cancellationToken)
    {
        var acceptedInvoiceIds = await dbContext.Set<SifecTransmission>()
            .Where(transmission => transmission.Status == SifecTransmissionStatus.Accepte)
            .Select(transmission => transmission.InvoiceId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var acceptedSet = acceptedInvoiceIds.ToHashSet();

        var candidateInvoiceIds = await dbContext.Set<Invoice>()
            .Where(invoice => invoice.Status == InvoiceStatus.Issued || invoice.Status == InvoiceStatus.Paid)
            .Select(invoice => invoice.Id)
            .ToArrayAsync(cancellationToken);

        var results = new List<SifecTransmissionResponse>();

        foreach (var invoiceId in candidateInvoiceIds.Where(id => !acceptedSet.Contains(id)))
        {
            var result = await SubmitAsync(invoiceId, context, cancellationToken);

            if (result.Succeeded && result.Value is not null)
            {
                results.Add(result.Value);
            }
        }

        return ApplicationResult<IReadOnlyCollection<SifecTransmissionResponse>>.Success(results);
    }

    public async Task<SifecConfigResponse> GetConfigAsync(CancellationToken cancellationToken)
    {
        var config = await LoadOrDefaultConfigAsync(cancellationToken);
        return Map(config);
    }

    public async Task<ApplicationResult<SifecConfigResponse>> SaveConfigAsync(
        SaveSifecConfigRequest request, OperationContext context, CancellationToken cancellationToken)
    {
        var config = await dbContext.Set<SifecConfig>()
            .SingleOrDefaultAsync(current => current.SingletonKey == SifecConfig.SingletonKeyValue, cancellationToken);

        var isNew = config is null;
        config ??= SifecConfig.CreateDefault();

        try
        {
            config.Update(request.Mode, request.ApiUrl, request.ApiKeyReference, request.DeclarantNif, request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<SifecConfigResponse>.Validation(ex.Message);
        }

        if (isNew)
        {
            config.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
            dbContext.Set<SifecConfig>().Add(config);
        }
        else
        {
            config.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        }

        await WriteAuditAsync(
            "fiscalite.sifec_config.saved", ConfigEntity, config.Id, context,
            new { Mode = config.Mode.ToString(), config.IsActive }, cancellationToken);

        return ApplicationResult<SifecConfigResponse>.Success(Map(config));
    }

    /// <summary>
    /// Sandbox always reports success (a real ping is meaningless: there is nothing to reach).
    /// Production always reports failure with an explicit message - see the class remarks.
    /// </summary>
    public async Task<ApplicationResult<SifecConfigResponse>> TestConnectionAsync(OperationContext context, CancellationToken cancellationToken)
    {
        var config = await dbContext.Set<SifecConfig>()
            .SingleOrDefaultAsync(current => current.SingletonKey == SifecConfig.SingletonKeyValue, cancellationToken);

        if (config is null)
        {
            return ApplicationResult<SifecConfigResponse>.Validation("Configure SIFEC before testing the connection.");
        }

        var succeeded = config.Mode == SifecMode.Sandbox;
        config.RecordConnectionTest(succeeded, DateTimeOffset.UtcNow);
        config.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "fiscalite.sifec_config.connection_tested", ConfigEntity, config.Id, context,
            new { Mode = config.Mode.ToString(), Succeeded = succeeded }, cancellationToken);

        return succeeded
            ? ApplicationResult<SifecConfigResponse>.Success(Map(config))
            : ApplicationResult<SifecConfigResponse>.Validation(ProductionNotImplementedMessage);
    }

    private async Task SubmitTransmissionAsync(SifecTransmission transmission, OperationContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        transmission.MarkSubmitted(now);

        if (transmission.Mode == SifecMode.Sandbox)
        {
            transmission.MarkAccepted($"SANDBOX-{Guid.NewGuid():N}", now);
        }
        else
        {
            // Volontaire : aucune integration API DGI production dans ce depot. Un succes
            // simule ici serait une fausse declaration de transmission reelle.
            transmission.MarkError(ProductionNotImplementedMessage, now);
        }

        await WriteAuditAsync(
            "fiscalite.sifec.submitted", SifecEntity, transmission.Id, context,
            new { transmission.InvoiceId, Status = transmission.Status.ToString(), transmission.Uid }, cancellationToken);
    }

    private async Task<SifecConfig> LoadOrDefaultConfigAsync(CancellationToken cancellationToken)
    {
        var config = await dbContext.Set<SifecConfig>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.SingletonKey == SifecConfig.SingletonKeyValue, cancellationToken);

        return config ?? SifecConfig.CreateDefault();
    }

    /// <summary>
    /// DGI-SIFEC-ALG v1.0 payload shape: issuer/receiver NIF, amounts and a document hash, plus a
    /// compact QR string built from the same fields - matching the legacy connector's contract.
    /// </summary>
    private static (string PayloadHash, string QrPayload) BuildPayload(Invoice invoice, SifecConfig config)
    {
        var canonical = string.Join('|',
            "DGI-SIFEC-ALG", "1.0",
            config.DeclarantNif ?? invoice.IssuerNifSnapshot ?? string.Empty,
            invoice.CustomerNifSnapshot ?? string.Empty,
            invoice.Number ?? string.Empty,
            invoice.TotalInclVat.ToString("F2"),
            DateTimeOffset.UtcNow.ToString("O"));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var qrPayload = $"SIFEC|{invoice.Number}|{invoice.TotalInclVat:F2}|{hash[..16]}";

        return (hash, qrPayload);
    }

    private static SifecTransmissionResponse Map(SifecTransmission transmission) => new(
        transmission.Id, transmission.InvoiceId, transmission.Status, transmission.Uid, transmission.Mode,
        transmission.SubmittedAt, transmission.ResponseMessage);

    private static SifecConfigResponse Map(SifecConfig config) => new(
        config.Mode, config.ApiUrl, config.ApiKeyReference, config.DeclarantNif, config.IsActive,
        config.LastConnectionTestAt, config.LastConnectionTestSucceeded);

    private async Task WriteAuditAsync(
        string action, string entityName, Guid entityId, OperationContext context, object details, CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(context.UserId, context.UserName, action, entityName, entityId.ToString(), context.IpAddress, JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
