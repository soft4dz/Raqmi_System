namespace RaqmiSystem.Application.Documents;

/// <summary>
/// En-tetes HTTP de la chaine documentaire, partages entre l'API qui les emet et le client Desktop
/// qui les lit : une seule constante, aucune chaine recopiee de part et d'autre.
/// </summary>
public static class DocumentHeaders
{
    /// <summary>Empreinte SHA-256 (hexadecimal minuscule) du corps servi, telle qu'archivee.</summary>
    public const string Sha256 = "X-Document-Sha256";
}
