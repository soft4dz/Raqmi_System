using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Maintenance;

/// <summary>
/// Volet applicatif des sauvegardes : lecture du dossier RAQMI_BACKUP_DIR alimente par la
/// tache serveur (deploy/onpremise/backup-raqmi.ps1) et declenchement d'un pg_dump a la
/// demande, avec les memes conventions et les memes garanties que le script :
///
///   * nom "raqmi_{base}_{yyyyMMdd_HHmmss}.dump" (BackupPolicy.BuildFileName), qui trie
///     chronologiquement - la retention du script en depend ;
///   * dump ecrit dans un fichier .part renomme seulement apres un exit 0 ET un fichier
///     non vide : un echec ne laisse jamais de dump partiel qui passerait pour valide ;
///   * jamais d'ecrasement d'un fichier existant ;
///   * mot de passe transmis par la variable d'environnement PGPASSWORD du processus fils,
///     jamais en argument de ligne de commande (les arguments sont visibles dans la liste
///     des processus).
///
/// SECURITE : la ligne de commande de pg_dump est construite EXCLUSIVEMENT a partir de la
/// configuration serveur (PostgresOptions, RAQMI_PG_BIN, RAQMI_BACKUP_DIR) et de valeurs
/// generees ici (nom de fichier horodate). Aucun parametre fourni par l'utilisateur ou par
/// la requete HTTP n'y participe : l'endpoint de declenchement ne prend d'ailleurs aucun
/// corps. Il n'existe donc aucun vecteur d'injection d'argument.
///
/// Configuration : les cles "BACKUP_DIR" et "PG_BIN" sont lues via IConfiguration, ou elles
/// arrivent depuis les variables d'environnement RAQMI_BACKUP_DIR et RAQMI_PG_BIN (Program.cs
/// fait AddEnvironmentVariables(prefix: "RAQMI_"), qui retire le prefixe) - les memes
/// variables que le script PowerShell lit depuis config\raqmi.env.ps1. Une configuration
/// absente est un etat a afficher (Configured=false) ou un refus Validation explicite,
/// jamais une exception 500.
///
/// La RESTAURATION n'existe volontairement pas ici : c'est une procedure d'administration
/// serveur documentee (docs/deployment-onpremise.md), pas un endpoint.
/// </summary>
public sealed class BackupService(
    IConfiguration configuration,
    IOptions<PostgresOptions> postgresOptions,
    IAuditLogWriter auditLogWriter) : IBackupService
{
    // Cles IConfiguration correspondant a RAQMI_BACKUP_DIR / RAQMI_PG_BIN une fois le
    // prefixe RAQMI_ retire par Program.cs.
    private const string BackupDirectoryKey = "BACKUP_DIR";
    private const string PgBinKey = "PG_BIN";

    // Timeout genereux : un pg_dump local de la base pilote se compte en secondes, mais une
    // base devenue volumineuse ou un disque lent ne doivent pas transformer une sauvegarde
    // legitime en echec. Au-dela, le processus est tue et le .part supprime.
    private static readonly TimeSpan DumpTimeout = TimeSpan.FromMinutes(15);

    public Task<BackupListResponse> ListBackupsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ListBackups());
    }

    public Task<BackupStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var list = ListBackups();

        var lastBackup = list.Backups
            .OrderByDescending(backup => backup.ModifiedAtUtc)
            .ThenBy(backup => backup.FileName, StringComparer.Ordinal)
            .FirstOrDefault();

        var utcNow = DateTimeOffset.UtcNow;
        double? ageHours = lastBackup is null ? null : (utcNow - lastBackup.ModifiedAtUtc).TotalHours;

        // Sur une installation configuree, l'absence totale de sauvegarde est le pire des
        // retards ; sur une installation non configuree, l'ecran affiche "non configure"
        // plutot qu'un retard qui n'aurait pas de sens.
        var isOverdue = list.Configured &&
            (lastBackup is null || BackupPolicy.IsOverdue(lastBackup.ModifiedAtUtc, utcNow));

        return Task.FromResult(new BackupStatusResponse(
            list.Configured,
            list.BackupDirectory,
            list.Backups.Count,
            lastBackup,
            ageHours,
            isOverdue,
            BackupPolicy.OverdueThreshold.TotalHours));
    }

    public async Task<ApplicationResult<TriggerBackupResponse>> TriggerBackupAsync(
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var backupRoot = configuration[BackupDirectoryKey];

        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            return ApplicationResult<TriggerBackupResponse>.Validation(
                "Backups are not configured on this server: the RAQMI_BACKUP_DIR environment " +
                "variable is not set. It is normally written to config\\raqmi.env.ps1 by " +
                "deploy/onpremise/install-server.ps1 (see docs/deployment-onpremise.md).");
        }

        var pgBin = configuration[PgBinKey];

        if (string.IsNullOrWhiteSpace(pgBin))
        {
            return ApplicationResult<TriggerBackupResponse>.Validation(
                "The RAQMI_PG_BIN environment variable is not set: it must point to the " +
                "PostgreSQL 'bin' directory containing pg_dump (see docs/deployment-onpremise.md).");
        }

        var pgDumpPath = Path.Combine(pgBin, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump");

        if (!File.Exists(pgDumpPath))
        {
            return ApplicationResult<TriggerBackupResponse>.Validation(
                $"pg_dump was not found at '{pgDumpPath}'. Check that RAQMI_PG_BIN points to the " +
                "PostgreSQL 'bin' directory of the installed server version.");
        }

        var options = postgresOptions.Value;
        var dailyDirectory = Path.Combine(backupRoot, "daily");

        // Nom entierement genere cote serveur : base + horodatage local (meme horloge que la
        // tache planifiee, pour que les fichiers des deux origines trient ensemble par nom).
        var fileName = BackupPolicy.BuildFileName(options.Database, DateTimeOffset.Now);
        var targetPath = Path.Combine(dailyDirectory, fileName);
        var partPath = targetPath + ".part";

        try
        {
            Directory.CreateDirectory(dailyDirectory);

            // Meme hygiene que le script : un .part restant est par definition un dump
            // partiel d'une execution tuee en plein vol - la retention ne matche que
            // *.dump, ils s'accumuleraient sinon pour toujours.
            foreach (var stalePart in Directory.EnumerateFiles(dailyDirectory, "*.dump.part"))
            {
                File.Delete(stalePart);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ApplicationResult<TriggerBackupResponse>.Validation(
                $"The backup directory '{dailyDirectory}' is not writable by the API process: {ex.Message}");
        }

        // L'horodatage est a la seconde : deux declenchements dans la meme seconde
        // viseraient le meme nom. On refuse plutot que d'ecraser - jamais d'ecrasement.
        if (File.Exists(targetPath))
        {
            return ApplicationResult<TriggerBackupResponse>.Conflict(
                $"A backup named '{fileName}' already exists. Wait a second and retry.");
        }

        var startInfo = new ProcessStartInfo(pgDumpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        // Arguments identiques a ceux du script serveur. Toutes les valeurs viennent de la
        // configuration serveur ou sont generees ci-dessus - aucune entree utilisateur.
        startInfo.ArgumentList.Add("-h");
        startInfo.ArgumentList.Add(options.Host);
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-U");
        startInfo.ArgumentList.Add(options.User);
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(options.Database);
        startInfo.ArgumentList.Add("-Fc");
        startInfo.ArgumentList.Add("--no-owner");
        startInfo.ArgumentList.Add("--no-privileges");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(partPath);

        // Mot de passe via l'environnement du processus fils uniquement - jamais en argument.
        startInfo.Environment["PGPASSWORD"] = options.Password;

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                DeleteQuietly(partPath);
                return ApplicationResult<TriggerBackupResponse>.Validation(
                    $"pg_dump could not be started from '{pgDumpPath}'.");
            }

            // stderr est lu en parallele de l'attente : pg_dump peut bloquer si le tampon
            // stderr se remplit sans lecteur.
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(DumpTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                DeleteQuietly(partPath);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return ApplicationResult<TriggerBackupResponse>.Validation(
                    $"pg_dump did not finish within {DumpTimeout.TotalMinutes:0} minutes and was " +
                    "stopped. No backup file was produced.");
            }

            var stderr = await stderrTask;
            await stdoutTask;

            if (process.ExitCode != 0)
            {
                DeleteQuietly(partPath);
                return ApplicationResult<TriggerBackupResponse>.Validation(
                    $"pg_dump failed (exit code {process.ExitCode}). {Summarize(stderr)}");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            DeleteQuietly(partPath);
            return ApplicationResult<TriggerBackupResponse>.Validation(
                $"pg_dump could not be started from '{pgDumpPath}': {ex.Message}");
        }

        // Memes verifications finales que le script : succes annonce mais fichier absent ou
        // vide = echec, et le .part n'est promu en .dump qu'apres ces controles.
        var partInfo = new FileInfo(partPath);

        if (!partInfo.Exists || partInfo.Length <= 0)
        {
            DeleteQuietly(partPath);
            return ApplicationResult<TriggerBackupResponse>.Validation(
                "pg_dump reported success but produced no usable backup file.");
        }

        try
        {
            // File.Move sans overwrite : si un fichier concurrent est apparu entre-temps,
            // l'IOException est traduite en conflit plutot qu'en ecrasement.
            File.Move(partPath, targetPath);
        }
        catch (IOException)
        {
            DeleteQuietly(partPath);
            return ApplicationResult<TriggerBackupResponse>.Conflict(
                $"A backup named '{fileName}' appeared concurrently. The new dump was discarded; retry.");
        }

        var completedAtUtc = DateTimeOffset.UtcNow;

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                "maintenance.backup.triggered",
                "maintenance.backups",
                fileName,
                context.IpAddress,
                JsonSerializer.Serialize(new { FileName = fileName, SizeBytes = partInfo.Length })),
            cancellationToken);

        return ApplicationResult<TriggerBackupResponse>.Success(
            new TriggerBackupResponse(fileName, partInfo.Length, completedAtUtc));
    }

    private BackupListResponse ListBackups()
    {
        var backupRoot = configuration[BackupDirectoryKey];

        // Variable absente : installation non configuree, pas d'erreur.
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            return new BackupListResponse(false, null, []);
        }

        // Variable definie mais dossier absent du disque : meme reponse calme, en gardant
        // le chemin pour que l'ecran puisse dire lequel des deux cas s'applique.
        if (!Directory.Exists(backupRoot))
        {
            return new BackupListResponse(false, backupRoot, []);
        }

        var backups = new List<BackupFileResponse>();

        foreach (var tier in BackupPolicy.Tiers)
        {
            var tierDirectory = Path.Combine(backupRoot, tier);

            if (!Directory.Exists(tierDirectory))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(tierDirectory, "*.dump"))
                {
                    var info = new FileInfo(path);

                    backups.Add(new BackupFileResponse(
                        info.Name,
                        tier,
                        info.Length,
                        new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un palier illisible (droits NTFS, disque retire) ne doit pas transformer
                // la consultation en 500 : les autres paliers restent listes.
            }
        }

        var ordered = backups
            .OrderByDescending(backup => backup.ModifiedAtUtc)
            .ThenBy(backup => backup.FileName, StringComparer.Ordinal)
            .ToArray();

        return new BackupListResponse(true, backupRoot, ordered);
    }

    private static string Summarize(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "No error output was produced.";
        }

        var trimmed = stderr.Trim();

        return trimmed.Length <= 2000 ? trimmed : trimmed[..2000] + "...";
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Deja termine, ou plus tuable : rien de mieux a faire.
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Le .part residuel sera nettoye par le prochain declenchement ou par le script.
        }
    }
}
