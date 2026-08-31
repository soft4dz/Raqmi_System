using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Volet applicatif du module Sauvegarde &amp; restauration : lecture de l'etat des
/// sauvegardes produites par la tache serveur (deploy/onpremise/backup-raqmi.ps1 /
/// deploy/backup/pg-backup.sh) et declenchement d'une sauvegarde a la demande.
///
/// La RESTAURATION n'a volontairement aucune methode ici : restaurer la base de
/// production est un acte d'administration serveur, execute a la main selon la
/// procedure documentee (docs/deployment-onpremise.md), jamais un bouton d'ecran.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Liste les fichiers .dump des trois paliers de retention sous RAQMI_BACKUP_DIR.
    /// Ne leve jamais : variable absente ou dossier inexistant donnent Configured=false
    /// et une liste vide.
    /// </summary>
    Task<BackupListResponse> ListBackupsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Derniere sauvegarde, son age et l'indicateur de retard (seuil BackupPolicy.OverdueThreshold).
    /// </summary>
    Task<BackupStatusResponse> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lance pg_dump -Fc vers le palier quotidien, avec les memes conventions de nommage
    /// et les memes garanties que le script serveur (fichier .part renomme apres succes,
    /// jamais d'ecrasement). Configuration manquante (RAQMI_BACKUP_DIR, RAQMI_PG_BIN,
    /// pg_dump introuvable) = resultat Validation explicite, pas d'erreur 500.
    /// </summary>
    Task<ApplicationResult<TriggerBackupResponse>> TriggerBackupAsync(
        OperationContext context,
        CancellationToken cancellationToken);
}
