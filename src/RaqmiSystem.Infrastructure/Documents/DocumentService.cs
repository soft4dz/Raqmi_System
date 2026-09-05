using System.Text.Json;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Documents;

namespace RaqmiSystem.Infrastructure.Documents;

/// <summary>
/// Orchestration de la chaine documentaire pour la facture : lire la facture PAR le module qui la
/// possede (IBillingService, jamais la table), servir l'archive si elle existe, sinon rendre,
/// archiver, auditer. Le service ne lit aucune entite Billing : le jour ou la facture change de
/// forme, c'est InvoiceResponse qui bouge, et ce service avec lui - pas une requete cachee ici.
/// </summary>
public sealed class DocumentService(
    IBillingService billingService,
    IApplicationSettingsService applicationSettingsService,
    IDocumentRenderer<InvoiceDocumentModel> invoiceRenderer,
    IDocumentArchive archive,
    IAuditLogWriter auditLogWriter) : IDocumentService
{
    public const string InvoiceRenderedAuditAction = "documents.invoice.rendered";

    public async Task<ApplicationResult<DocumentContentResponse>> GetInvoiceDocumentAsync(
        Guid invoiceId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var result = await GetOrRenderInvoiceDocumentAsync(invoiceId, context, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return Propagate<DocumentContentResponse>(result);
        }

        return ApplicationResult<DocumentContentResponse>.Success(
            new DocumentContentResponse(DocumentMetadataResponse.From(result.Value), result.Value.Content));
    }

    public async Task<ApplicationResult<DocumentMetadataResponse>> GetInvoiceDocumentMetadataAsync(
        Guid invoiceId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var result = await GetOrRenderInvoiceDocumentAsync(invoiceId, context, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return Propagate<DocumentMetadataResponse>(result);
        }

        return ApplicationResult<DocumentMetadataResponse>.Success(DocumentMetadataResponse.From(result.Value));
    }

    private async Task<ApplicationResult<RenderedDocument>> GetOrRenderInvoiceDocumentAsync(
        Guid invoiceId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var invoiceResult = await billingService.GetInvoiceAsync(invoiceId, cancellationToken);

        if (!invoiceResult.Succeeded || invoiceResult.Value is null)
        {
            return ApplicationResult<RenderedDocument>.NotFound(invoiceResult.Error ?? "La facture est introuvable.");
        }

        var invoice = invoiceResult.Value;

        // Un brouillon n'a pas de numero, donc pas de document legal ; une facture annulee alors
        // qu'elle etait encore brouillon n'en a pas non plus (jamais numerotee). 404 et non 409 :
        // du point de vue du demandeur, le document n'existe pas.
        if (string.IsNullOrWhiteSpace(invoice.Number) || invoice.Status == InvoiceStatus.Draft)
        {
            return ApplicationResult<RenderedDocument>.NotFound(
                "Un brouillon n'a pas de document legal : la facture doit d'abord etre emise.");
        }

        var archived = await archive.FindAsync(DocumentType.Invoice, invoice.Number, cancellationToken);

        if (archived is not null)
        {
            return ApplicationResult<RenderedDocument>.Success(archived);
        }

        // Premier rendu. La fiche client peut manquer (client desactive, code orphelin) : le
        // document s'imprime alors avec la raison sociale figee sur la facture et sans adresse,
        // plutot que de refuser une piece legale deja emise.
        var customerResult = await billingService.GetCustomerAsync(invoice.CustomerCode, cancellationToken);
        var settings = await applicationSettingsService.GetAsync(cancellationToken);

        var model = InvoiceDocumentModelBuilder.Build(
            invoice,
            customerResult.Succeeded ? customerResult.Value : null,
            settings);

        var content = invoiceRenderer.Render(model);

        var rendered = RenderedDocument.Create(
            DocumentType.Invoice,
            invoice.Number,
            invoiceRenderer.TemplateVersion,
            content,
            context.UserName,
            DateTimeOffset.UtcNow);

        var stored = await archive.StoreAsync(rendered, cancellationToken);

        // Seul le rendu effectivement archive est audite : un rendu concurrent perdant (premier
        // ecrit gagne) n'a rien produit de durable et ne doit pas apparaitre comme une remise.
        if (ReferenceEquals(stored, rendered))
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    context.UserId,
                    context.UserName,
                    InvoiceRenderedAuditAction,
                    nameof(RenderedDocument),
                    stored.Id.ToString(),
                    context.IpAddress,
                    JsonSerializer.Serialize(new
                    {
                        invoiceId,
                        number = invoice.Number,
                        sha256 = stored.Sha256,
                        templateVersion = stored.TemplateVersion,
                        sizeBytes = stored.SizeBytes
                    })),
                cancellationToken);
        }

        return ApplicationResult<RenderedDocument>.Success(stored);
    }

    private static ApplicationResult<T> Propagate<T>(ApplicationResult<RenderedDocument> failure)
    {
        var message = failure.Error ?? "Le document est indisponible.";

        return failure.ErrorType switch
        {
            ApplicationErrorType.NotFound => ApplicationResult<T>.NotFound(message),
            ApplicationErrorType.Conflict => ApplicationResult<T>.Conflict(message),
            _ => ApplicationResult<T>.Validation(message)
        };
    }
}
