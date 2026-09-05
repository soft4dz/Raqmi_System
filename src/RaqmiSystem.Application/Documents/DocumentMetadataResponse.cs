using RaqmiSystem.Domain.Documents;

namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Ce qu'un client sait d'un document archive sans le telecharger : de quoi verifier l'empreinte
/// d'un fichier deja recu, nommer le fichier et dater la remise.
/// </summary>
public sealed record DocumentMetadataResponse(
    Guid Id,
    DocumentType Type,
    string Reference,
    int TemplateVersion,
    string Sha256,
    long SizeBytes,
    string ContentType,
    string FileName,
    DateTimeOffset RenderedAt,
    string RenderedBy)
{
    public static DocumentMetadataResponse From(RenderedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new DocumentMetadataResponse(
            document.Id,
            document.Type,
            document.Reference,
            document.TemplateVersion,
            document.Sha256,
            document.SizeBytes,
            document.ContentType,
            document.FileName,
            document.RenderedAt,
            document.RenderedBy);
    }
}
