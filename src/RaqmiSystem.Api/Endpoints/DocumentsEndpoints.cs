using Microsoft.Net.Http.Headers;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Chaine documentaire : remise des pieces legales. Lire une facture et lire son document sont le
/// meme droit (billing.invoice.read, couvert par la cle historique invoices.read) : le document
/// ne revele rien que l'ecran facture ne montre deja.
/// </summary>
internal static class DocumentsEndpoints
{
    public static RouteGroupBuilder MapDocumentsEndpoints(this RouteGroupBuilder api)
    {
        var documents = api.MapGroup("/documents")
            .WithTags("Documents");

        // Le PDF lui-meme. Premier appel : rendu + archivage ; appels suivants : l'archive, octet
        // pour octet. L'empreinte voyage en en-tete pour que le client puisse verifier ce qu'il a
        // recu avant de l'imprimer, et en ETag pour les caches HTTP.
        documents.MapGet("/invoices/{invoiceId:guid}", async (
            Guid invoiceId,
            IDocumentService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetInvoiceDocumentAsync(
                invoiceId,
                httpContext.ToOperationContext(),
                cancellationToken);

            if (!result.Succeeded || result.Value is null)
            {
                return result.ToHttpResult();
            }

            var document = result.Value;

            httpContext.Response.Headers[DocumentHeaders.Sha256] = document.Metadata.Sha256;

            return Results.File(
                document.Content,
                document.Metadata.ContentType,
                document.Metadata.FileName,
                lastModified: document.Metadata.RenderedAt,
                entityTag: new EntityTagHeaderValue($"\"{document.Metadata.Sha256}\""));
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceRead);

        documents.MapGet("/invoices/{invoiceId:guid}/metadata", async (
            Guid invoiceId,
            IDocumentService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetInvoiceDocumentMetadataAsync(
                invoiceId,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceRead);

        return api;
    }
}
