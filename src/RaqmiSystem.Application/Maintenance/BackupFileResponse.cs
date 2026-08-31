namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Un fichier de sauvegarde present sur le disque du serveur. <paramref name="Tier"/> est le
/// nom du sous-dossier de retention ("daily", "weekly" ou "monthly" - voir BackupPolicy.Tiers) ;
/// <paramref name="ModifiedAtUtc"/> est la date d'ecriture du fichier, source la plus fiable
/// de l'age d'une sauvegarde (le nom la duplique mais un fichier renomme a la main mentirait).
/// </summary>
public sealed record BackupFileResponse(
    string FileName,
    string Tier,
    long SizeBytes,
    DateTimeOffset ModifiedAtUtc);
