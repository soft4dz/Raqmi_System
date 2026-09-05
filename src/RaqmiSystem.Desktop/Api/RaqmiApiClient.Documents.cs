using System.Net.Http;
using System.Security.Cryptography;
using RaqmiSystem.Application.Documents;

namespace RaqmiSystem.Desktop.Api;

// Chaine documentaire : appels du groupe /api/v1/documents (DocumentsEndpoints).
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec les
// autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string DocumentsPath = "/api/v1/documents";

    /// <summary>
    /// Telecharge le PDF de la facture (rendu au premier appel, archive ensuite : le serveur
    /// renvoie toujours la meme piece). Le nom de fichier vient du Content-Disposition du serveur
    /// - il porte le numero legal - et l'empreinte annoncee en en-tete est conservee pour que la
    /// fenetre d'apercu verifie ce qu'elle a recu avant de l'imprimer.
    /// </summary>
    public async Task<DownloadedDocument> DownloadInvoicePdfAsync(
        string apiBaseUrl,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        using var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{DocumentsPath}/invoices/{invoiceId}",
            null,
            includeAuthorization: true,
            cancellationToken);

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"facture-{invoiceId:N}.pdf";
        }

        var serverSha256 = response.Headers.TryGetValues(DocumentHeaders.Sha256, out var values)
            ? values.FirstOrDefault()
            : null;

        return new DownloadedDocument(
            fileName,
            response.Content.Headers.ContentType?.MediaType ?? "application/pdf",
            content,
            serverSha256);
    }

    /// <summary>Les metadonnees du document (empreinte, taille, date de rendu) sans le telecharger.</summary>
    public async Task<DocumentMetadataResponse> GetInvoiceDocumentMetadataAsync(
        string apiBaseUrl,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{DocumentsPath}/invoices/{invoiceId}/metadata",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<DocumentMetadataResponse>(response, cancellationToken);
    }
}

/// <summary>
/// Un document recu du serveur, avec l'empreinte calculee localement et celle annoncee par le
/// serveur : la fenetre d'apercu refuse d'imprimer une piece dont les deux divergent.
/// </summary>
public sealed record DownloadedDocument(
    string FileName,
    string ContentType,
    byte[] Content,
    string? ServerSha256)
{
    public string LocalSha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(Content));

    /// <summary>true : empreintes identiques ; false : divergence ; null : le serveur n'en a pas annonce.</summary>
    public bool? IntegrityVerified =>
        ServerSha256 is null
            ? null
            : string.Equals(LocalSha256, ServerSha256, StringComparison.OrdinalIgnoreCase);
}
