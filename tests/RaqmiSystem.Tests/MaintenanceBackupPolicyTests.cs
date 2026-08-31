using RaqmiSystem.Application.Maintenance;

namespace RaqmiSystem.Tests;

/// <summary>
/// Regles pures du module sauvegarde : convention de nommage identique aux scripts serveur
/// et seuil de retard exact aux bornes. Ce sont ces regles que le service et l'ecran
/// consomment - une derive ici casserait la retention des scripts (qui trie par nom).
/// </summary>
public sealed class MaintenanceBackupPolicyTests
{
    [Fact]
    public void File_name_follows_the_server_scripts_convention()
    {
        // backup-raqmi.ps1 : 'raqmi_{0}_{1}.dump' -f $database, $now.ToString('yyyyMMdd_HHmmss')
        var timestamp = new DateTimeOffset(2026, 8, 30, 3, 30, 15, TimeSpan.FromHours(1));

        var fileName = BackupPolicy.BuildFileName("raqmi_system", timestamp);

        Assert.Equal("raqmi_raqmi_system_20260830_033015.dump", fileName);
    }

    [Fact]
    public void File_names_sort_chronologically_which_the_retention_scripts_depend_on()
    {
        var earlier = BackupPolicy.BuildFileName("raqmi_system", new DateTimeOffset(2026, 8, 30, 23, 59, 59, TimeSpan.Zero));
        var later = BackupPolicy.BuildFileName("raqmi_system", new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.True(string.CompareOrdinal(earlier, later) < 0);
    }

    [Fact]
    public void Threshold_is_the_documented_26_hours()
    {
        // 24 h de cadence quotidienne (tache a 03:30) + 2 h de marge.
        Assert.Equal(TimeSpan.FromHours(26), BackupPolicy.OverdueThreshold);
    }

    [Fact]
    public void Exactly_26_hours_old_is_still_on_time()
    {
        var last = new DateTimeOffset(2026, 8, 29, 3, 30, 0, TimeSpan.Zero);
        var now = last + TimeSpan.FromHours(26);

        // Borne incluse cote "a jour" : le retard est STRICTEMENT au-dela du seuil.
        Assert.False(BackupPolicy.IsOverdue(last, now));
    }

    [Fact]
    public void One_second_past_26_hours_is_overdue()
    {
        var last = new DateTimeOffset(2026, 8, 29, 3, 30, 0, TimeSpan.Zero);
        var now = last + TimeSpan.FromHours(26) + TimeSpan.FromSeconds(1);

        Assert.True(BackupPolicy.IsOverdue(last, now));
    }

    [Fact]
    public void A_fresh_backup_is_on_time()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.False(BackupPolicy.IsOverdue(now - TimeSpan.FromHours(1), now));
    }

    [Fact]
    public void Tiers_match_the_directories_created_by_the_server_scripts()
    {
        Assert.Equal(["daily", "weekly", "monthly"], BackupPolicy.Tiers);
    }
}
