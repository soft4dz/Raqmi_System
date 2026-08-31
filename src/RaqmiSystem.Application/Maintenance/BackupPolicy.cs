namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Regles pures du module sauvegarde, partagees entre le service, l'ecran et les tests.
///
/// Elles refletent la strategie reelle des scripts serveur (deploy/onpremise/backup-raqmi.ps1
/// et deploy/backup/pg-backup.sh) : dump pg_dump -Fc nomme "raqmi_{base}_{yyyyMMdd_HHmmss}.dump",
/// range dans trois paliers de retention (daily / weekly / monthly). Toute divergence de
/// convention entre ce module et les scripts casserait la retention (elle trie par nom).
/// </summary>
public static class BackupPolicy
{
    /// <summary>
    /// La tache planifiee "Raqmi System Backup" tourne chaque jour a 03:30. Une sauvegarde
    /// plus vieille que 24 h signifie donc qu'une execution a ete manquee ; les 2 h de marge
    /// absorbent une execution longue ou un rattrapage apres redemarrage du serveur.
    /// Seuil documente : 26 h = 24 h (cadence quotidienne) + 2 h de marge.
    /// </summary>
    public static readonly TimeSpan OverdueThreshold = TimeSpan.FromHours(26);

    /// <summary>
    /// Les trois sous-dossiers de retention crees par les scripts serveur, dans l'ordre
    /// de frequence. Les noms sont ceux des dossiers sur disque, jamais traduits ici :
    /// la traduction est une affaire d'affichage.
    /// </summary>
    public static readonly IReadOnlyList<string> Tiers = ["daily", "weekly", "monthly"];

    /// <summary>
    /// Une sauvegarde est en retard STRICTEMENT au-dela du seuil : a exactement 26 h
    /// elle est encore consideree a jour (borne incluse cote "a jour").
    /// </summary>
    public static bool IsOverdue(DateTimeOffset lastBackupUtc, DateTimeOffset utcNow)
    {
        return utcNow - lastBackupUtc > OverdueThreshold;
    }

    /// <summary>
    /// Nom de fichier identique a celui des scripts serveur : "raqmi_{base}_{yyyyMMdd_HHmmss}.dump".
    /// L'horodatage trie chronologiquement par nom, ce dont la retention des scripts depend.
    /// Le nom est entierement genere cote serveur : aucune entree utilisateur n'y participe.
    /// </summary>
    public static string BuildFileName(string database, DateTimeOffset timestamp)
    {
        return $"raqmi_{database}_{timestamp:yyyyMMdd_HHmmss}.dump";
    }
}
