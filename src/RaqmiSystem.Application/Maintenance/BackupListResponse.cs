namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Liste des sauvegardes trouvees sous RAQMI_BACKUP_DIR. <paramref name="Configured"/> est
/// faux quand la variable n'est pas definie OU quand le dossier qu'elle designe n'existe pas :
/// dans les deux cas la reponse reste un 200 avec une liste vide - une installation sans
/// sauvegarde configuree est un etat legitime a AFFICHER, jamais une erreur serveur.
/// <paramref name="BackupDirectory"/> porte le chemin configure (meme absent du disque),
/// ou null quand la variable n'est pas definie, pour que l'ecran puisse dire lequel des
/// deux cas s'applique.
/// </summary>
public sealed record BackupListResponse(
    bool Configured,
    string? BackupDirectory,
    IReadOnlyCollection<BackupFileResponse> Backups);
