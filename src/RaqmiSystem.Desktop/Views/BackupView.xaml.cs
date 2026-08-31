using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Sauvegarde et restauration (volet applicatif) : etat de la derniere sauvegarde,
/// liste des fichiers par palier de retention, declenchement d'une sauvegarde immediate.
/// La restauration n'a AUCUN bouton : c'est une procedure d'administration serveur
/// documentee (docs/deployment-onpremise.md), l'ecran se contente de le dire.
/// Vue autonome : elle ne connait que le ModuleViewContext que la fenetre lui prete.
/// </summary>
public partial class BackupView : UserControl
{
    private const string TriggerPermission = PermissionCatalog.MaintenanceBackup;

    private const string TriggerPermissionHint =
        "Permission maintenance.backup requise : seul l'administrateur système peut déclencher une sauvegarde.";

    private ModuleViewContext? context;

    // Le profil connecte peut-il declencher une sauvegarde ? Memorise a l'ouverture de
    // session ; le serveur reste la seule autorite en matiere de droits.
    private bool canTriggerBackup;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par le
    // message de permission : l'affectation doit rester symetrique (ApplyPermissionHint).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    public BackupView()
    {
        InitializeComponent();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canTriggerBackup = context.HasPermission(TriggerPermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge l'etat et la liste. Sort silencieusement tant qu'aucun contexte n'est
    /// disponible ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadAsync);
    }

    /// <summary>Vide la grille et les indicateurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        BackupsDataGrid.ItemsSource = null;
        BackupCountTextBlock.Text = string.Empty;
        LastBackupValueTextBlock.Text = "—";
        LastBackupFileTextBlock.Text = string.Empty;
        AgeCaptionTextBlock.Text = string.Empty;
        BackupDirectoryTextBlock.Text = "—";
        NotConfiguredBorder.Visibility = Visibility.Collapsed;
        SetAgeBadge("—", "StatusDraft");
        UpdateActionButtons();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadAsync();
            moduleContext.SetStatus("État des sauvegardes actualisé.");
        });
    }

    private async void TriggerBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        // Confirmation legere : l'acte n'est pas destructeur (il AJOUTE un fichier),
        // mais il occupe le serveur plusieurs secondes et ecrit sur son disque.
        var confirmed = Confirm(
            "Lancer une sauvegarde de la base de données maintenant ?\n" +
            "Le fichier sera écrit dans le palier quotidien du serveur ; l'opération peut prendre plusieurs minutes.",
            "Sauvegarder maintenant");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var result = await moduleContext.ApiClient.TriggerBackupAsync(moduleContext.ApiBaseUrl);

            await ReloadAsync();

            moduleContext.SetStatus(
                $"Sauvegarde {result.FileName} créée ({FormatMegabytes(result.SizeBytes)} Mo).");
        });
    }

    private async Task ReloadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var status = await moduleContext.ApiClient.GetBackupStatusAsync(moduleContext.ApiBaseUrl);
        var list = await moduleContext.ApiClient.GetBackupsAsync(moduleContext.ApiBaseUrl);

        RenderStatus(status);
        RenderList(list);
    }

    private void RenderStatus(BackupStatusResponse status)
    {
        RenderConfiguration(status.Configured, status.BackupDirectory);

        if (status.LastBackup is null)
        {
            LastBackupValueTextBlock.Text = "Aucune";
            LastBackupFileTextBlock.Text = string.Empty;
            AgeCaptionTextBlock.Text = string.Empty;

            if (!status.Configured)
            {
                SetAgeBadge("Non configuré", "StatusRejected");
            }
            else
            {
                SetAgeBadge("Aucune sauvegarde", "StatusRejected");
            }

            return;
        }

        // Horodatage UTC converti en heure du poste avant affichage (regle 3.8).
        var localTime = status.LastBackup.ModifiedAtUtc.ToLocalTime();
        LastBackupValueTextBlock.Text = localTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        LastBackupFileTextBlock.Text =
            $"{status.LastBackup.FileName} — {FormatMegabytes(status.LastBackup.SizeBytes)} Mo";

        var ageHours = status.AgeHours ?? 0;
        AgeCaptionTextBlock.Text = DescribeAge(ageHours);

        // Le retard fait foi cote SERVEUR (IsOverdue, seuil renvoye dans la reponse) :
        // l'ecran n'invente pas de regle. La seule gradation locale est visuelle :
        // un retard qui depasse le seuil de plus de 48 h passe d'ambre a rouge.
        if (!status.IsOverdue)
        {
            SetAgeBadge("À jour", "StatusValidated");
        }
        else if (ageHours <= status.OverdueThresholdHours + 48)
        {
            SetAgeBadge("En retard", "StatusSubmitted");
        }
        else
        {
            SetAgeBadge("En retard important", "StatusRejected");
        }
    }

    /// <summary>
    /// Le serveur distingue soigneusement DEUX etats non configures, et l'ecran doit les
    /// distinguer aussi car la marche a suivre n'est pas la meme :
    ///
    ///   * variable RAQMI_BACKUP_DIR absente (BackupDirectory null) : il faut la DEFINIR ;
    ///   * variable definie mais dossier absent du disque (BackupDirectory renseigne, mais
    ///     Configured faux) : le chemin est connu, il faut CREER le dossier ou corriger la
    ///     variable - dire ici "non configuré" enverrait l'administrateur redefinir une
    ///     variable deja definie.
    /// </summary>
    private void RenderConfiguration(bool configured, string? backupDirectory)
    {
        BackupDirectoryTextBlock.Text = backupDirectory ?? "RAQMI_BACKUP_DIR non défini sur le serveur";

        if (configured)
        {
            NotConfiguredBorder.Visibility = Visibility.Collapsed;
            return;
        }

        NotConfiguredBorder.Visibility = Visibility.Visible;

        if (backupDirectory is null)
        {
            NotConfiguredTitleTextBlock.Text = "Dossier de sauvegarde non configuré sur le serveur";
            NotConfiguredDetailTextBlock.Text =
                "Définissez la variable RAQMI_BACKUP_DIR (et RAQMI_PG_BIN) dans le fichier " +
                "config\\raqmi.env.ps1 du serveur — l'installeur deploy\\onpremise\\install-server.ps1 " +
                "le fait automatiquement. Procédure complète : docs/deployment-onpremise.md, " +
                "section « Backups ».";

            return;
        }

        NotConfiguredTitleTextBlock.Text = "Dossier de sauvegarde introuvable sur le serveur";
        NotConfiguredDetailTextBlock.Text =
            $"La variable RAQMI_BACKUP_DIR désigne « {backupDirectory} », mais ce dossier n'existe " +
            "pas sur le disque du serveur : aucune sauvegarde n'y est écrite ni lue. Créez ce " +
            "dossier (ou corrigez le chemin dans config\\raqmi.env.ps1), puis relancez la tâche " +
            "planifiée « Raqmi System Backup ». Procédure complète : docs/deployment-onpremise.md, " +
            "section « Backups ».";
    }

    private void RenderList(BackupListResponse list)
    {
        var rows = list.Backups.Select(ToRowView).ToArray();
        BackupsDataGrid.ItemsSource = rows;

        BackupCountTextBlock.Text = rows.Length == 1
            ? "1 sauvegarde"
            : $"{rows.Length.ToString(CultureInfo.CurrentCulture)} sauvegardes";
    }

    // Les boutons d'ecriture sont grises sans le droit, avec info-bulle posee ET
    // restauree (motif ApplyPermissionHint : les vues survivent a la deconnexion).
    private void UpdateActionButtons()
    {
        TriggerBackupButton.IsEnabled = canTriggerBackup;
        ApplyPermissionHint(TriggerBackupButton, canTriggerBackup, TriggerPermissionHint);
    }

    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non.
    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    // Teintes semantiques exclusivement via les brushes Status* du theme - jamais de
    // couleur en dur dans le code-behind.
    private void SetAgeBadge(string text, string statusKey)
    {
        AgeBadgeTextBlock.Text = text;
        AgeBadgeBorder.Background = (Brush)FindResource(statusKey + "BackgroundBrush");
        AgeBadgeTextBlock.Foreground = (Brush)FindResource(statusKey + "ForegroundBrush");
    }

    private static string DescribeAge(double ageHours)
    {
        if (ageHours < 1)
        {
            return "il y a moins d'une heure";
        }

        if (ageHours < 48)
        {
            var hours = (int)Math.Floor(ageHours);
            return hours == 1 ? "il y a 1 heure" : $"il y a {hours.ToString(CultureInfo.CurrentCulture)} heures";
        }

        var days = (int)Math.Floor(ageHours / 24);
        return $"il y a {days.ToString(CultureInfo.CurrentCulture)} jours";
    }

    private static string FormatMegabytes(long sizeBytes)
    {
        // Montants et tailles : N2 de la culture courante (regle 3.7).
        return (sizeBytes / 1048576d).ToString("N2", CultureInfo.CurrentCulture);
    }

    // Libelle francais du palier : source unique pour la grille (le meme mot partout).
    private static string DescribeTier(string tier)
    {
        return tier switch
        {
            "daily" => "Quotidienne",
            "weekly" => "Hebdomadaire",
            "monthly" => "Mensuelle",
            _ => tier
        };
    }

    private static BackupRowView ToRowView(BackupFileResponse backup)
    {
        return new BackupRowView(
            backup.FileName,
            backup.Tier,
            DescribeTier(backup.Tier),
            backup.SizeBytes,
            (backup.SizeBytes / 1048576d).ToString("N2", CultureInfo.CurrentCulture),
            backup.ModifiedAtUtc,
            backup.ModifiedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));
    }

    private sealed record BackupRowView(
        string FileName,
        string Tier,
        string TierLabel,
        long SizeBytes,
        string SizeMbLabel,
        DateTimeOffset ModifiedAtUtc,
        string ModifiedLabel);
}
