namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Etat synthetique des sauvegardes : la derniere sauvegarde (tous paliers confondus), son
/// age en heures, et l'indicateur de retard calcule cote serveur avec le seuil documente de
/// BackupPolicy (26 h : cadence quotidienne + marge). Le seuil est renvoye pour que l'ecran
/// affiche la meme regle que le serveur au lieu d'en recopier la valeur.
/// <paramref name="IsOverdue"/> est vrai des que le dossier est configure mais qu'aucune
/// sauvegarde assez recente n'existe - y compris quand il n'y en a aucune.
/// </summary>
public sealed record BackupStatusResponse(
    bool Configured,
    string? BackupDirectory,
    int BackupCount,
    BackupFileResponse? LastBackup,
    double? AgeHours,
    bool IsOverdue,
    double OverdueThresholdHours);
