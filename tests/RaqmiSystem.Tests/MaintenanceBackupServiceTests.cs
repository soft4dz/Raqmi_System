using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Maintenance;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Tests du service de sauvegarde sur un systeme de fichiers temporaire construit par le
/// test lui-meme : lecture des trois paliers, installation non configuree qui repond
/// calmement (jamais d'exception), statut d'age, et refus explicites du declenchement
/// quand la configuration attendue manque.
///
/// Le lancement REEL de pg_dump est teste via un faux executable... la ou c'est possible :
/// un script shell fait un pg_dump credible sous Linux (ou la CI execute ces tests), mais
/// sous Windows CreateProcess exige un vrai executable PE - un .cmd ou .ps1 ne peut pas se
/// faire passer pour pg_dump.exe sans UseShellExecute, que le service n'utilise pas (a
/// raison). Les deux tests concernes sortent donc honnetement en debut de methode sous
/// Windows ; le chemin reel reste couvert par la CI (ubuntu-latest, .github/workflows/dotnet.yml).
/// </summary>
public sealed class MaintenanceBackupServiceTests : IDisposable
{
    private readonly List<string> tempDirectories = [];

    // ------------------------------------------------------------------ ListBackupsAsync

    [Fact]
    public async Task Lists_the_three_retention_tiers_with_size_and_date()
    {
        var root = CreateTempDirectory();
        var now = DateTime.UtcNow;

        WriteBackup(root, "daily", "raqmi_raqmi_system_20260830_033000.dump", 300, now.AddHours(-2));
        WriteBackup(root, "weekly", "raqmi_raqmi_system_20260824_033000.dump", 200, now.AddDays(-6));
        WriteBackup(root, "monthly", "raqmi_raqmi_system_20260801_033000.dump", 100, now.AddDays(-29));

        var service = CreateService(backupDir: root, pgBin: null);

        var list = await service.ListBackupsAsync(CancellationToken.None);

        Assert.True(list.Configured);
        Assert.Equal(root, list.BackupDirectory);
        Assert.Equal(3, list.Backups.Count);

        // Tries du plus recent au plus ancien, tous paliers confondus.
        var ordered = list.Backups.ToArray();
        Assert.Equal("daily", ordered[0].Tier);
        Assert.Equal("weekly", ordered[1].Tier);
        Assert.Equal("monthly", ordered[2].Tier);

        var daily = ordered[0];
        Assert.Equal("raqmi_raqmi_system_20260830_033000.dump", daily.FileName);
        Assert.Equal(300, daily.SizeBytes);
        Assert.Equal(new DateTimeOffset(now.AddHours(-2)), daily.ModifiedAtUtc, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Ignores_files_that_are_not_dump_files()
    {
        var root = CreateTempDirectory();
        WriteBackup(root, "daily", "raqmi_raqmi_system_20260830_033000.dump", 10, DateTime.UtcNow);
        WriteBackup(root, "daily", "raqmi_raqmi_system_20260830_040000.dump.part", 5, DateTime.UtcNow);
        WriteBackup(root, "daily", "notes.txt", 5, DateTime.UtcNow);

        var service = CreateService(backupDir: root, pgBin: null);

        var list = await service.ListBackupsAsync(CancellationToken.None);

        // Un .part est un dump partiel d'une execution echouee, jamais une sauvegarde.
        Assert.Single(list.Backups);
        Assert.Equal("raqmi_raqmi_system_20260830_033000.dump", list.Backups.Single().FileName);
    }

    [Fact]
    public async Task Missing_environment_variable_reports_not_configured_without_failing()
    {
        var service = CreateService(backupDir: null, pgBin: null);

        var list = await service.ListBackupsAsync(CancellationToken.None);

        Assert.False(list.Configured);
        Assert.Null(list.BackupDirectory);
        Assert.Empty(list.Backups);
    }

    [Fact]
    public async Task Missing_directory_reports_not_configured_but_keeps_the_configured_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "raqmi-tests-" + Guid.NewGuid().ToString("N"));

        var service = CreateService(backupDir: missing, pgBin: null);

        var list = await service.ListBackupsAsync(CancellationToken.None);

        Assert.False(list.Configured);
        Assert.Equal(missing, list.BackupDirectory);
        Assert.Empty(list.Backups);
    }

    [Fact]
    public async Task Tolerates_missing_tier_subdirectories()
    {
        var root = CreateTempDirectory();
        WriteBackup(root, "daily", "raqmi_raqmi_system_20260830_033000.dump", 10, DateTime.UtcNow);
        // Ni weekly ni monthly : le dossier vient d'etre configure, la tache n'a pas
        // encore promu de sauvegarde.

        var service = CreateService(backupDir: root, pgBin: null);

        var list = await service.ListBackupsAsync(CancellationToken.None);

        Assert.True(list.Configured);
        Assert.Single(list.Backups);
    }

    // ------------------------------------------------------------------- GetStatusAsync

    [Fact]
    public async Task A_fresh_backup_is_reported_on_time_with_its_age()
    {
        var root = CreateTempDirectory();
        WriteBackup(root, "daily", "raqmi_raqmi_system_20260830_033000.dump", 10, DateTime.UtcNow.AddHours(-2));

        var service = CreateService(backupDir: root, pgBin: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Configured);
        Assert.NotNull(status.LastBackup);
        Assert.False(status.IsOverdue);
        Assert.NotNull(status.AgeHours);
        Assert.InRange(status.AgeHours!.Value, 1.9, 2.1);
        Assert.Equal(BackupPolicy.OverdueThreshold.TotalHours, status.OverdueThresholdHours);
    }

    [Fact]
    public async Task A_backup_older_than_the_threshold_is_reported_overdue()
    {
        var root = CreateTempDirectory();
        WriteBackup(root, "daily", "raqmi_raqmi_system_20260828_033000.dump", 10, DateTime.UtcNow.AddHours(-30));

        var service = CreateService(backupDir: root, pgBin: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsOverdue);
        Assert.InRange(status.AgeHours!.Value, 29.9, 30.1);
    }

    [Fact]
    public async Task The_most_recent_file_across_all_tiers_is_the_last_backup()
    {
        var root = CreateTempDirectory();
        var now = DateTime.UtcNow;
        WriteBackup(root, "daily", "raqmi_raqmi_system_old.dump", 10, now.AddHours(-40));
        WriteBackup(root, "monthly", "raqmi_raqmi_system_new.dump", 10, now.AddHours(-1));

        var service = CreateService(backupDir: root, pgBin: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal("raqmi_raqmi_system_new.dump", status.LastBackup!.FileName);
        Assert.False(status.IsOverdue);
    }

    [Fact]
    public async Task A_configured_directory_with_no_backup_at_all_is_overdue()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "daily"));

        var service = CreateService(backupDir: root, pgBin: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Configured);
        Assert.Null(status.LastBackup);
        Assert.Null(status.AgeHours);
        Assert.True(status.IsOverdue);
    }

    [Fact]
    public async Task An_unconfigured_installation_is_not_reported_overdue()
    {
        var service = CreateService(backupDir: null, pgBin: null);

        var status = await service.GetStatusAsync(CancellationToken.None);

        // L'ecran affiche "non configure" : un indicateur de retard n'aurait pas de sens.
        Assert.False(status.Configured);
        Assert.False(status.IsOverdue);
    }

    // ---------------------------------------------------------------- TriggerBackupAsync

    [Fact]
    public async Task Trigger_is_refused_with_an_explanation_when_the_backup_directory_is_not_configured()
    {
        var audit = new RecordingAuditWriter();
        var service = CreateService(backupDir: null, pgBin: null, audit);

        var result = await service.TriggerBackupAsync(OperationContext.System, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaqmiSystem.Application.Common.ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("RAQMI_BACKUP_DIR", result.Error);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Trigger_is_refused_when_the_postgres_bin_directory_is_not_configured()
    {
        var root = CreateTempDirectory();
        var service = CreateService(backupDir: root, pgBin: null);

        var result = await service.TriggerBackupAsync(OperationContext.System, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaqmiSystem.Application.Common.ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("RAQMI_PG_BIN", result.Error);
    }

    [Fact]
    public async Task Trigger_is_refused_when_pg_dump_is_not_found_in_the_configured_directory()
    {
        var root = CreateTempDirectory();
        var emptyBin = CreateTempDirectory();
        var service = CreateService(backupDir: root, pgBin: emptyBin);

        var result = await service.TriggerBackupAsync(OperationContext.System, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaqmiSystem.Application.Common.ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("pg_dump", result.Error);
        Assert.Contains("RAQMI_PG_BIN", result.Error);
    }

    [Fact]
    public async Task Trigger_runs_pg_dump_writes_the_daily_file_and_audits_the_action()
    {
        if (OperatingSystem.IsWindows())
        {
            // Voir l'en-tete de la classe : impossible de simuler pg_dump.exe par un script
            // sous Windows sans UseShellExecute. Chemin couvert par la CI Linux.
            return;
        }

        var root = CreateTempDirectory();
        var bin = CreateTempDirectory();
        WriteFakePgDump(bin, exitCode: 0);

        var audit = new RecordingAuditWriter();
        var service = CreateService(backupDir: root, pgBin: bin, audit);

        var result = await service.TriggerBackupAsync(
            new OperationContext(Guid.NewGuid(), "admin.test", "127.0.0.1"),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);
        Assert.StartsWith("raqmi_raqmi_system_", result.Value!.FileName);
        Assert.EndsWith(".dump", result.Value.FileName);
        Assert.True(result.Value.SizeBytes > 0);

        var dailyDir = Path.Combine(root, "daily");
        var produced = Path.Combine(dailyDir, result.Value.FileName);
        Assert.True(File.Exists(produced));

        // Aucun .part residuel : le fichier temporaire a ete promu apres succes.
        Assert.Empty(Directory.GetFiles(dailyDir, "*.part"));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("maintenance.backup.triggered", entry.Action);
        Assert.Equal("maintenance.backups", entry.EntityName);
        Assert.Equal(result.Value.FileName, entry.EntityId);
        Assert.Equal("admin.test", entry.UserName);
    }

    [Fact]
    public async Task A_failing_pg_dump_returns_its_stderr_and_leaves_no_partial_file()
    {
        if (OperatingSystem.IsWindows())
        {
            // Meme limitation que ci-dessus ; chemin couvert par la CI Linux.
            return;
        }

        var root = CreateTempDirectory();
        var bin = CreateTempDirectory();
        WriteFakePgDump(bin, exitCode: 3, stderr: "connection to server failed: boom");

        var audit = new RecordingAuditWriter();
        var service = CreateService(backupDir: root, pgBin: bin, audit);

        var result = await service.TriggerBackupAsync(OperationContext.System, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaqmiSystem.Application.Common.ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("exit code 3", result.Error);
        Assert.Contains("boom", result.Error);

        var dailyDir = Path.Combine(root, "daily");
        Assert.Empty(Directory.GetFiles(dailyDir));
        Assert.Empty(audit.Entries);
    }

    // ------------------------------------------------------------------------- Plomberie

    private static BackupService CreateService(
        string? backupDir,
        string? pgBin,
        RecordingAuditWriter? audit = null)
    {
        var values = new Dictionary<string, string?>();

        // Les cles nues correspondent a RAQMI_BACKUP_DIR / RAQMI_PG_BIN une fois le prefixe
        // RAQMI_ retire par AddEnvironmentVariables(prefix: "RAQMI_") dans Program.cs.
        if (backupDir is not null)
        {
            values["BACKUP_DIR"] = backupDir;
        }

        if (pgBin is not null)
        {
            values["PG_BIN"] = pgBin;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new BackupService(
            configuration,
            Options.Create(new PostgresOptions { Database = "raqmi_system", User = "raqmi_app", Password = "secret" }),
            audit ?? new RecordingAuditWriter());
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "raqmi-backup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        tempDirectories.Add(path);
        return path;
    }

    private static void WriteBackup(string root, string tier, string fileName, int sizeBytes, DateTime lastWriteUtc)
    {
        var directory = Path.Combine(root, tier);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    /// <summary>
    /// Faux pg_dump POSIX : recupere la valeur de l'argument -f, y ecrit un contenu
    /// non vide (exit 0) ou ecrit sur stderr et sort avec le code demande.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static void WriteFakePgDump(string binDirectory, int exitCode, string? stderr = null)
    {
        var path = Path.Combine(binDirectory, "pg_dump");

        string script;

        if (exitCode == 0)
        {
            script = "#!/bin/sh\n" +
                "out=\"\"\n" +
                "while [ $# -gt 0 ]; do\n" +
                "  if [ \"$1\" = \"-f\" ]; then out=\"$2\"; fi\n" +
                "  shift\n" +
                "done\n" +
                "printf 'FAKE PGDMP CONTENT' > \"$out\"\n" +
                "exit 0\n";
        }
        else
        {
            script = "#!/bin/sh\n" +
                $"echo '{stderr}' 1>&2\n" +
                $"exit {exitCode}\n";
        }

        File.WriteAllText(path, script);
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        foreach (var directory in tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Un dossier temporaire non supprimable ne doit pas faire echouer le test.
            }
        }
    }
}
