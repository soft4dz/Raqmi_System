using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Sync;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module 29 - Registre des postes et erreurs clients.
///
/// Vue INTEGRALEMENT EN LECTURE SEULE : aucun bouton d'ecriture, aucun acte engageant, donc aucune
/// confirmation a demander. La seule permission en jeu est sync.read, portee par l'onglet lui-meme
/// dans MainWindow ; il n'y a rien ici a griser selon le profil.
///
/// Regle de vocabulaire tenue partout dans ce fichier : jamais "en ligne", jamais "connecte". Le
/// serveur ne sait pas qu'un poste s'est eteint. Ce qui est affiche est un DERNIER CONTACT et une
/// fraicheur calculee par le serveur, avec ses propres seuils, renvoyes dans la reponse.
/// Vue autonome : elle ne connait que le ModuleViewContext que la fenetre lui prete.
/// </summary>
public partial class SyncView : UserControl
{
    private const int FailurePageSize = 100;

    private ModuleViewContext? context;

    public SyncView()
    {
        InitializeComponent();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;

        // L'identite de CE poste est purement locale : elle s'affiche meme hors session, ce qui
        // aide au diagnostic quand la connexion ne s'etablit pas.
        RenderThisStation();
    }

    /// <summary>
    /// (Re)charge le registre et le journal. Sort silencieusement tant qu'aucun contexte n'est
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

    /// <summary>Vide les grilles et les indicateurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        StationsDataGrid.ItemsSource = null;
        FailuresDataGrid.ItemsSource = null;
        StationCountTextBlock.Text = "—";
        StaleCountTextBlock.Text = "—";
        ThresholdCaptionTextBlock.Text = string.Empty;
        VersionBadgeTextBlock.Text = "—";
        VersionCaptionTextBlock.Text = string.Empty;
        ServerTimeTextBlock.Text = string.Empty;
        StationsCaptionTextBlock.Text = string.Empty;
        FailuresCaptionTextBlock.Text = string.Empty;
        RenderThisStation();
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
            moduleContext.SetStatus("Registre des postes actualisé.");
        });
    }

    private async void IncludeAllKnownCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Declenche aussi au chargement du XAML, avant Initialize : on sort sans rien faire.
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var includeAllKnown = IncludeAllKnownCheckBox.IsChecked == true;

        var registry = await moduleContext.ApiClient.GetWorkstationsAsync(
            moduleContext.ApiBaseUrl,
            includeAllKnown);

        var failures = await moduleContext.ApiClient.GetWorkstationFailuresAsync(
            moduleContext.ApiBaseUrl,
            FailurePageSize);

        RenderRegistry(registry);
        RenderFailures(failures);
    }

    private void RenderRegistry(WorkstationRegistryResponse registry)
    {
        var rows = registry.Workstations
            .Select(station => new StationRow(station))
            .ToList();

        StationsDataGrid.ItemsSource = rows;

        StationCountTextBlock.Text = rows.Count.ToString(CultureInfo.CurrentCulture);

        // "Sans contact recent" agrege Stale ET Silent : du point de vue du lecteur, les deux
        // veulent dire "ce poste ne s'est pas manifeste depuis un moment".
        var quiet = rows.Count(row => !string.Equals(row.Freshness, "Recent", StringComparison.Ordinal));
        StaleCountTextBlock.Text = quiet.ToString(CultureInfo.CurrentCulture);

        ThresholdCaptionTextBlock.Text =
            $"Au-delà de {registry.StaleAfterMinutes} min, puis silencieux au-delà de {registry.OfflineAfterMinutes} min.";

        VersionBadgeTextBlock.Text = registry.DistinctAppVersions switch
        {
            0 => "—",
            1 => "1 version",
            _ => $"{registry.DistinctAppVersions} versions"
        };

        VersionCaptionTextBlock.Text = registry.DistinctAppVersions > 1
            ? "Plusieurs versions du client sont en service : à aligner."
            : string.Empty;

        // Heure SERVEUR, affichee en heure locale du lecteur : les âges de cet écran sont
        // calculés par le serveur, pas par l'horloge du poste qui consulte.
        ServerTimeTextBlock.Text =
            $"Heure serveur : {registry.ServerTimeUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}";

        StationsCaptionTextBlock.Text = IncludeAllKnownCheckBox.IsChecked == true
            ? "Tous les postes connus"
            : "Postes vus depuis 30 jours";
    }

    private void RenderFailures(IReadOnlyCollection<WorkstationFailureResponse> failures)
    {
        var rows = failures.Select(failure => new FailureRow(failure)).ToList();

        FailuresDataGrid.ItemsSource = rows;

        FailuresCaptionTextBlock.Text = rows.Count == 0
            ? string.Empty
            : $"{rows.Count} dernière(s) erreur(s) signalée(s)";
    }

    private void RenderThisStation()
    {
        // Le nom de machine est affiche a cote de la version parce que l'identite du poste est
        // liee au PROFIL WINDOWS et non a la machine : deux sessions Windows sur un meme PC
        // apparaissent comme deux postes. Montrer les deux rend l'ecart lisible.
        ThisStationTextBlock.Text = $"{StationIdentity.Label} — version {StationIdentity.AppVersion}";
    }

    /// <summary>Ligne du registre, mise en forme pour l'affichage.</summary>
    private sealed class StationRow(WorkstationResponse station)
    {
        public string Label { get; } = station.Label;

        public string LastUserName { get; } = station.LastUserName;

        public string AppVersion { get; } = station.AppVersion;

        public string HotelUnitLabel { get; } = station.HotelUnitCode ?? "—";

        public string Freshness { get; } = station.Freshness;

        public int MinutesSinceLastContact { get; } = station.MinutesSinceLastContact;

        public string FreshnessLabel { get; } = BuildFreshnessLabel(station);

        public string LastSeenLabel { get; } = FormatInstant(station.LastSeenUtc);

        public string FirstSeenLabel { get; } = FormatInstant(station.FirstSeenUtc);

        // Aucun de ces libelles n'affirme qu'un poste est allume : ils decrivent l'anciennete du
        // dernier contact, ce qui est tout ce que le serveur peut honnetement savoir.
        private static string BuildFreshnessLabel(WorkstationResponse station)
        {
            var minutes = station.MinutesSinceLastContact;

            return station.Freshness switch
            {
                "Recent" => minutes <= 1 ? "Contact à l'instant" : $"Contact il y a {minutes} min",
                "Stale" => $"Sans contact ({FormatDuration(minutes)})",
                _ => $"Silencieux ({FormatDuration(minutes)})"
            };
        }
    }

    /// <summary>Ligne du journal des erreurs, mise en forme pour l'affichage.</summary>
    private sealed class FailureRow(WorkstationFailureResponse failure)
    {
        public string WorkstationLabel { get; } = failure.WorkstationLabel;

        public string KindLabel { get; } = failure.Kind switch
        {
            "Network" => "Réseau injoignable",
            "Timeout" => "Délai dépassé",
            "HttpError" => "Erreur serveur",
            _ => "Imprévue"
        };

        public string CallLabel { get; } = failure.StatusCode is { } code
            ? $"{failure.Method} {failure.Path} → {code}"
            : $"{failure.Method} {failure.Path}";

        public string Message { get; } = failure.Message;

        public string RecordedLabel { get; } = FormatInstant(failure.RecordedAtUtc);

        public int ClockDriftSeconds { get; } = failure.ClockDriftSeconds;

        // Un ecart d'horloge important est un constat d'exploitation a part entiere : un poste
        // mal a l'heure produit de mauvaises dates metier partout ailleurs dans le produit.
        public string ClockDriftLabel { get; } = Math.Abs(failure.ClockDriftSeconds) < 120
            ? "—"
            : $"{(failure.ClockDriftSeconds > 0 ? "retard" : "avance")} {FormatDuration(Math.Abs(failure.ClockDriftSeconds) / 60)}";
    }

    private static string FormatInstant(DateTimeOffset instant)
    {
        return instant.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    private static string FormatDuration(int minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        if (minutes < 60 * 24)
        {
            return $"{minutes / 60} h";
        }

        return $"{minutes / (60 * 24)} j";
    }
}
