namespace RaqmiSystem.Application.Maintenance;

/// <summary>
/// Resultat d'une sauvegarde declenchee a la demande : le fichier produit dans le palier
/// quotidien, sa taille, et l'instant de fin. Le nom est genere cote serveur (BackupPolicy),
/// jamais fourni par l'appelant.
/// </summary>
public sealed record TriggerBackupResponse(
    string FileName,
    long SizeBytes,
    DateTimeOffset CompletedAtUtc);
