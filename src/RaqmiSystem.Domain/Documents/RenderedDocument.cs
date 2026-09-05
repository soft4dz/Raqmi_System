using System.Security.Cryptography;

namespace RaqmiSystem.Domain.Documents;

/// <summary>
/// Un document legal tel qu'il a ete rendu et remis : la piece, octet pour octet.
///
/// IMMUABLE ET UNIQUE PAR (type, reference). La premiere demande rend et archive ; toute demande
/// suivante renvoie l'archive telle quelle. Un document emis ne se re-rend jamais : le gabarit
/// peut evoluer, le parametrage de l'emetteur peut changer, la fiche du client peut etre
/// corrigee - la facture FAC-2026-000012 remise au client reste celle qui a ete remise. C'est la
/// base de donnees qui tient cette regle (index unique sur type + reference), pas la discipline
/// des appelants.
///
/// L'entite ne porte volontairement ni UpdatedAt ni UpdatedBy : rien ne se met a jour.
/// </summary>
public sealed class RenderedDocument
{
    public const string PdfContentType = "application/pdf";

    /// <summary>SHA-256 en hexadecimal minuscule : 32 octets, 64 caracteres.</summary>
    public const int Sha256HexLength = 64;

    public const int ReferenceMaxLength = 60;

    /// <summary>Signature d'en-tete de tout fichier PDF (ISO 32000, 7.5.2).</summary>
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    private RenderedDocument()
    {
        Reference = string.Empty;
        Sha256 = string.Empty;
        RenderedBy = string.Empty;
        Content = [];
    }

    private RenderedDocument(
        DocumentType type,
        string reference,
        int templateVersion,
        byte[] content,
        string renderedBy,
        DateTimeOffset renderedAt)
    {
        Type = type;
        Reference = reference;
        TemplateVersion = templateVersion;
        Content = content;
        SizeBytes = content.LongLength;
        Sha256 = ComputeSha256(content);
        RenderedBy = renderedBy;
        RenderedAt = renderedAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public DocumentType Type { get; private set; }

    /// <summary>
    /// Reference metier du document : pour une facture, son numero legal (FAC-AAAA-NNNNNN). C'est
    /// par elle qu'un document se reimprime, et c'est elle que l'index unique protege.
    /// </summary>
    public string Reference { get; private set; }

    /// <summary>
    /// Version du gabarit qui a produit le contenu. Ne sert pas a re-rendre (on ne re-rend pas) :
    /// elle dit avec quelle mise en page une piece ancienne a ete remise, ce qu'un controle peut
    /// demander.
    /// </summary>
    public int TemplateVersion { get; private set; }

    /// <summary>Empreinte SHA-256 du contenu, hexadecimal minuscule. Renvoyee au client en en-tete.</summary>
    public string Sha256 { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset RenderedAt { get; private set; }

    public string RenderedBy { get; private set; }

    /// <summary>Le PDF lui-meme (colonne bytea).</summary>
    public byte[] Content { get; private set; }

    public string ContentType => PdfContentType;

    /// <summary>Nom de fichier propose au client : la reference porte deja le numero.</summary>
    public string FileName => Reference + ".pdf";

    public static RenderedDocument Create(
        DocumentType type,
        string reference,
        int templateVersion,
        ReadOnlySpan<byte> content,
        string renderedBy,
        DateTimeOffset renderedAt)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type.");
        }

        if (templateVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(templateVersion), templateVersion, "Template version must be at least 1.");
        }

        if (content.IsEmpty)
        {
            throw new ArgumentException("A rendered document cannot be empty.", nameof(content));
        }

        // Un archivage n'a de sens que pour une piece lisible : un contenu qui n'est pas un PDF
        // serait servi avec le mauvais type et resterait fige a jamais par l'index unique.
        if (!content.StartsWith(PdfSignature))
        {
            throw new ArgumentException("A rendered document must be a PDF file.", nameof(content));
        }

        // Copie defensive : l'appelant garde son tampon, l'archive garde le sien. Personne ne
        // peut modifier apres coup les octets qui vont etre archives.
        return new RenderedDocument(
            type,
            NormalizeReference(reference),
            templateVersion,
            content.ToArray(),
            RequireActor(renderedBy),
            renderedAt);
    }

    public static string ComputeSha256(ReadOnlySpan<byte> content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>
    /// La reference est la cle d'unicite : elle est normalisee (espaces, casse) pour qu'une meme
    /// piece ne puisse pas etre archivee deux fois sous deux graphies.
    /// </summary>
    public static string NormalizeReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Document reference is required.", nameof(reference));
        }

        var trimmed = reference.Trim().ToUpperInvariant();

        if (trimmed.Length > ReferenceMaxLength)
        {
            throw new ArgumentException($"Document reference cannot exceed {ReferenceMaxLength} characters.", nameof(reference));
        }

        return trimmed;
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }
}
