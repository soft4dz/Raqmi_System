using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Documents;

/// <summary>
/// La chaine documentaire vue des endpoints : « donne-moi le document de cette facture ». Les deux
/// operations sont idempotentes et partagent le meme chemin - la premiere demande rend et archive,
/// les suivantes lisent l'archive - de sorte que les metadonnees decrivent toujours la piece qui
/// sera servie, jamais une piece « a venir ».
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Le PDF de la facture, avec ses metadonnees. NotFound si la facture n'existe pas OU si elle
    /// est encore un brouillon : un brouillon n'a pas de numero, donc pas de document legal.
    /// </summary>
    Task<ApplicationResult<DocumentContentResponse>> GetInvoiceDocumentAsync(
        Guid invoiceId,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Les metadonnees seules (empreinte, taille, version de gabarit, date de rendu).</summary>
    Task<ApplicationResult<DocumentMetadataResponse>> GetInvoiceDocumentMetadataAsync(
        Guid invoiceId,
        OperationContext context,
        CancellationToken cancellationToken);
}
