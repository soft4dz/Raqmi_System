using System.Net.Http;
using RaqmiSystem.Application.Maintenance;

namespace RaqmiSystem.Desktop.Api;

// Module Sauvegarde & restauration : appels du groupe /api/v1/maintenance/backups.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string BackupsPath = "/api/v1/maintenance/backups";

    /// <summary>
    /// Liste les sauvegardes des trois paliers (daily/weekly/monthly). Une installation
    /// sans dossier configure repond Configured=false avec une liste vide, jamais une erreur.
    /// </summary>
    public async Task<BackupListResponse> GetBackupsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, BackupsPath, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BackupListResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Etat synthetique : derniere sauvegarde, age, indicateur de retard calcule par le
    /// serveur avec son propre seuil (renvoye dans la reponse).
    /// </summary>
    public async Task<BackupStatusResponse> GetBackupStatusAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{BackupsPath}/status", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<BackupStatusResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Declenche une sauvegarde immediate (permission maintenance.backup). Aucun parametre :
    /// le serveur genere lui-meme le nom et l'emplacement du fichier.
    /// </summary>
    public async Task<TriggerBackupResponse> TriggerBackupAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{BackupsPath}/trigger", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<TriggerBackupResponse>(response, cancellationToken);
    }
}
