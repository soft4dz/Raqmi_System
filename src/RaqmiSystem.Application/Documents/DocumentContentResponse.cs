namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Le document a servir : ses metadonnees et ses octets. Consomme par l'endpoint qui repond en
/// application/pdf ; jamais serialise en JSON (le contenu passerait en base64 pour rien).
/// </summary>
public sealed record DocumentContentResponse(
    DocumentMetadataResponse Metadata,
    byte[] Content);
