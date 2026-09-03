using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// « Mon travail » : l'etabli de l'onglet 0.
/// </summary>
/// <remarks>
/// Contrat de vue de la charte § 2.1 : <see cref="Initialize"/> ne fait aucun appel
/// reseau, <see cref="LoadAsync"/> sort hors session, <see cref="ResetState"/> vide tout
/// a la deconnexion, et la vue ne connait pas <c>MainWindow</c> - elle demande une
/// navigation, la fenetre decide.
///
/// Chargement : le composeur dit QUELLES files sont lisibles et DANS QUEL ORDRE appeler
/// les sources ; la vue affiche aussitot les cartes en etat « Chargement », puis fait UNE
/// <c>context.RunAsync</c> PAR SOURCE, de la plus legere a la plus lourde. Aucun try/catch
/// reseau ici : <c>RunApiActionAsync</c> traduit deja l'erreur en message d'etat, la vue
/// ne sait qu'une chose - si son delegue a pose le drapeau. Une source en echec bascule
/// SES cartes en « Indisponible » et n'arrete pas les suivantes.
/// </remarks>
public partial class WorkQueuesView : UserControl
{
    private const int SettingsTab = 9;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    private readonly ObservableCollection<HomeCardModel> overdueCards = [];
    private readonly ObservableCollection<HomeCardModel> todayCards = [];
    private readonly ObservableCollection<HomeCardModel> watchCards = [];

    private ModuleViewContext? context;
    private Func<IReadOnlySet<string>> grantedKeys = () => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Func<int, bool> canOpenModule = _ => false;

    private string? stationUnitCode;
    private string? currencyLabel;
    private DateTimeOffset? lastLoadedUtc;
    private bool isLoading;
    private bool unitsLoaded;

    public WorkQueuesView()
    {
        InitializeComponent();
    }

    /// <summary>Ouverture d'un ecran demandee par une carte ou une puce.</summary>
    public event Action<int>? NavigateRequested;

    /// <summary>« Ma sécurité » : la fenetre ouvre sa boite de changement de mot de passe.</summary>
    public event Action? ChangePasswordRequested;

    /// <summary>« Ouvrir le catalogue des modules » : l'hote bascule de section.</summary>
    public event Action? OpenCatalogRequested;

    /// <summary>
    /// Ce que la fenetre sait des le demarrage : les cles courantes du profil et le garde
    /// de navigation, qui filtre les derniers ecrans du poste.
    /// </summary>
    public void InitializeNavigation(Func<IReadOnlySet<string>> permissionKeys, Func<int, bool> canOpen)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        ArgumentNullException.ThrowIfNull(canOpen);

        grantedKeys = permissionKeys;
        canOpenModule = canOpen;
    }

    /// <summary>Contrat § 2.1 : aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext viewContext)
    {
        ArgumentNullException.ThrowIfNull(viewContext);

        context = viewContext;
    }

    /// <summary>Salutation et date : ce que la connexion apprend, avant tout appel.</summary>
    public void OpenSession(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        GreetingTextBlock.Text = $"Bonjour, {user.DisplayName}";
        RefreshDateLine();
    }

    public async Task LoadAsync()
    {
        if (context is null || !context.ApiClient.IsAuthenticated || isLoading)
        {
            return;
        }

        isLoading = true;

        try
        {
            stationUnitCode = DesktopSettings.LoadStationUnitCode();

            var layout = HomeComposer.Compose(grantedKeys(), stationUnitCode is not null);
            var results = new HomeSourceResults();
            var cards = RenderSkeleton(layout);

            RefreshBanner(layout);
            RefreshRecentScreens();
            RefreshProductCard();

            // L'etablissement AVANT les files, et attendu : il porte le libelle de devise
            // dont les montants des cartes ont besoin, et deux RunAsync concurrentes se
            // marcheraient sur les pieds (SetBusy est un compteur binaire, la premiere qui
            // finit rallume MainTabs pendant que l'autre est encore en vol).
            await RefreshEstablishmentAsync(layout);

            // Une RunAsync PAR source, de la plus legere a la plus lourde (l'ordre de
            // l'enumeration) : les cartes se remplissent au fil des reponses.
            foreach (var source in layout.Sources)
            {
                var ok = false;

                await context.RunAsync(async () =>
                {
                    await FetchAsync(source, results);
                    ok = true;                       // pose seulement si l'appel a abouti
                });

                if (!ok)
                {
                    results.Failed.Add(source);      // RunAsync a deja affiche l'erreur
                }

                ProjectSource(source, cards, results);
            }

            RefreshFailedSourcesBanner(cards, results);
            RefreshSummary(layout, cards);
            RefreshBands(layout);

            lastLoadedUtc = DateTimeOffset.UtcNow;
            RefreshFreshness();
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// Retour sur l'onglet 0 : on recharge au-dela de cinq minutes, la cadence du battement
    /// de poste. Un compteur perime est pire qu'un rechargement borne et annonce ; et un
    /// <c>Timer</c> ferait des appels que personne n'a demandes.
    /// </summary>
    public Task RefreshIfStaleAsync() =>
        lastLoadedUtc is { } last && DateTimeOffset.UtcNow - last <= StaleAfter
            ? Task.CompletedTask
            : LoadAsync();

    /// <summary>
    /// Deconnexion : la vue survit et resservira au profil suivant, donc rien de la session
    /// precedente ne doit subsister. Les reglages du POSTE, eux, restent.
    /// </summary>
    public void ResetState()
    {
        GreetingTextBlock.Text = "Bonjour";
        RefreshDateLine();

        overdueCards.Clear();
        todayCards.Clear();
        watchCards.Clear();
        OverdueBandPanel.Children.Clear();
        TodayBandPanel.Children.Clear();
        WatchBandPanel.Children.Clear();

        SummaryTextBlock.Text = "Chargement des files de travail…";
        EstablishmentTextBlock.Visibility = Visibility.Collapsed;
        StationUnitPanel.Visibility = Visibility.Collapsed;
        BusinessDatePanel.Visibility = Visibility.Collapsed;
        UnitMissingBanner.Visibility = Visibility.Collapsed;
        StationUnitEditor.Visibility = Visibility.Collapsed;
        FailedSourcesBanner.Visibility = Visibility.Collapsed;
        RecentScreensPanel.Visibility = Visibility.Collapsed;
        FreshnessTextBlock.Text = string.Empty;

        currencyLabel = null;
        lastLoadedUtc = null;
        unitsLoaded = false;
        StationUnitComboBox.ItemsSource = null;
    }

    /// <summary>Un ecran vient d'etre ouvert : il passe en tete des derniers ecrans du poste.</summary>
    public void RecordVisit(int tabIndex)
    {
        DesktopSettings.SaveRecentTab(tabIndex);
        RefreshRecentScreens();
    }

    // ======================== Composition et rendu des bandes ========================

    // Une carte par slot, en etat « Chargement » : la page a sa forme definitive avant le
    // premier appel, donc rien ne saute sous le curseur quand les reponses arrivent.
    private Dictionary<string, HomeCardModel> RenderSkeleton(HomeLayout layout)
    {
        overdueCards.Clear();
        todayCards.Clear();
        watchCards.Clear();

        var cards = new Dictionary<string, HomeCardModel>(StringComparer.Ordinal);

        foreach (var slot in layout.Slots)
        {
            var card = new HomeCardModel(slot, ScopeLabel(slot), TargetLabel(slot.TargetTab))
            {
                IconKey = IconKeyFor(slot.TargetTab)
            };

            cards[slot.Queue.Id] = card;
            CollectionFor(slot.Queue.Band).Add(card);
        }

        RenderBand(OverdueBandPanel, HomeBand.Overdue, "En retard", layout);
        RenderBand(TodayBandPanel, HomeBand.Today, "Aujourd'hui", layout);
        RenderBand(WatchBandPanel, HomeBand.Watch, "À surveiller", layout);

        return cards;
    }

    private ObservableCollection<HomeCardModel> CollectionFor(HomeBand band) => band switch
    {
        HomeBand.Overdue => overdueCards,
        HomeBand.Today => todayCards,
        _ => watchCards
    };

    // Une bande = un en-tete (mot + point + compteur de CARTES) et, dessous, ses cartes ou
    // son etat vide. L'etat vide dit POURQUOI il est vide : « aucune file ouverte a votre
    // profil » n'est pas « rien a traiter », et une bande qui n'a rien lu ne pretend jamais
    // que tout est en ordre.
    private void RenderBand(Panel host, HomeBand band, string title, HomeLayout layout)
    {
        host.Children.Clear();

        var section = layout.Band(band);
        var cards = CollectionFor(band);
        var header = BuildBandHeader(band, title, cards);
        host.Children.Add(header);

        if (cards.Count == 0)
        {
            // Deux vides tres differents, et la nuance est le coeur de l'honnetete de cet
            // ecran : « rien n'a ete lu » (aucune file n'est ouverte au profil, ou l'unite
            // du poste manque) ne doit JAMAIS s'afficher comme « tout est en ordre ».
            // EmptyReason.None avec zero carte veut dire l'inverse : des files ont bien ete
            // composees et lues, et le serveur n'a rien renvoye a traiter.
            switch (section.EmptyReason)
            {
                case HomeEmptyReason.UnitMissing when band == HomeBand.Today:
                    host.Children.Add(BuildEmptyBox(
                        "Rien à traiter",
                        "Vos files dépendent d'une unité : fixez l'unité de ce poste dans l'encart ci-dessus."));
                    break;

                case HomeEmptyReason.NoQueues when band == HomeBand.Today:
                    host.Children.Add(BuildEmptyBox(
                        "Rien à traiter",
                        "Aucune file de travail n'est ouverte à votre profil. Vos écrans restent accessibles par la barre latérale et le catalogue."));
                    break;

                case HomeEmptyReason.UnitMissing:
                case HomeEmptyReason.NoQueues:
                    // En retard et À surveiller se contentent d'une ligne attenuee : trois
                    // boites d'etat vide sur une page en feraient un mur de vide.
                    header.Opacity = 0.7;
                    host.Children.Add(new TextBlock
                    {
                        Text = section.EmptyReason == HomeEmptyReason.UnitMissing
                            ? "Dépend de l'unité de ce poste, non définie."
                            : "Aucune file ouverte à votre profil.",
                        Style = TryFindResource("CaptionText") as Style,
                        Margin = new Thickness(2, 0, 0, 0)
                    });
                    break;

                default:
                    host.Children.Add(BuildEmptyBox(AllClearTitle(band), AllClearHint(band)));
                    break;
            }

            return;
        }

        var items = new ItemsControl
        {
            ItemsSource = cards,
            ItemTemplate = (DataTemplate)FindResource("HomeWorkCardTemplate"),
            ItemsPanel = WrapPanelTemplate(),
            Margin = new Thickness(0, 4, 0, 0)
        };

        host.Children.Add(items);
    }

    // WrapPanel comme panneau d'items : declare une fois en code plutot que copie dans
    // trois ItemsControl identiques.
    private static ItemsPanelTemplate WrapPanelTemplate() =>
        new(new FrameworkElementFactory(typeof(WrapPanel)));

    private FrameworkElement BuildBandHeader(HomeBand band, string title, ObservableCollection<HomeCardModel> cards)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        row.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = BandBrushKey(band),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var label = new TextBlock
        {
            Text = title,
            Style = TryFindResource("HomeSectionLabel") as Style,
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center
        };

        AutomationProperties.SetHeadingLevel(label, AutomationHeadingLevel.Level2);
        AutomationProperties.SetName(label, $"{title}, {FileCountLabel(cards.Count)}");
        row.Children.Add(label);

        if (cards.Count > 0)
        {
            var pill = new Border
            {
                Background = TryFindResource("SurfaceSubtleBrush") as System.Windows.Media.Brush,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = FileCountLabel(cards.Count),
                    FontSize = 11,
                    Foreground = TryFindResource("TextSecondaryBrush") as System.Windows.Media.Brush
                }
            };

            row.Children.Add(pill);
        }

        // « suivi seulement » quand aucune carte de la bande ne porte de verbe : le lecteur
        // n'est jamais somme d'agir sur ce qu'il ne peut pas traiter.
        if (cards.Count > 0 && cards.All(card => !card.IsAct))
        {
            row.Children.Add(new TextBlock
            {
                Text = "· suivi seulement",
                Style = TryFindResource("CaptionText") as Style,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            });
        }

        return row;
    }

    private System.Windows.Media.Brush? BandBrushKey(HomeBand band) => band switch
    {
        HomeBand.Overdue => TryFindResource("StatusSubmittedForegroundBrush") as System.Windows.Media.Brush,
        HomeBand.Today => TryFindResource("AccentBrush") as System.Windows.Media.Brush,
        _ => TryFindResource("TextMutedBrush") as System.Windows.Media.Brush
    };

    // Des files ont ete lues et le serveur n'a rien a signaler : la bande peut enfin le
    // dire. Les libelles nomment ce qui APPARAITRAIT ici, jamais des tâches, des
    // notifications ou des messages - aucun de ces services n'existe cote serveur.
    private static string AllClearTitle(HomeBand band) => band switch
    {
        HomeBand.Overdue => "Rien en retard",
        HomeBand.Today => "Rien à traiter",
        _ => "Rien à surveiller"
    };

    private static string AllClearHint(HomeBand band) => band switch
    {
        HomeBand.Overdue => "Les arrivées, départs, clôtures, rejets et sauvegardes en retard apparaîtront ici.",
        HomeBand.Today => "Vos files de travail du jour apparaîtront ici.",
        _ => "Chambres hors service, articles sous le minimum et postes apparaîtront ici."
    };

    private FrameworkElement BuildEmptyBox(string title, string hint)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            Style = TryFindResource("EmptyStateTitleText") as Style
        });

        panel.Children.Add(new TextBlock
        {
            Text = hint,
            Style = TryFindResource("EmptyStateHintText") as Style,
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            BorderBrush = TryFindResource("PanelBorderBrush") as System.Windows.Media.Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 18, 20, 18),
            Margin = new Thickness(0, 6, 0, 0),
            IsHitTestVisible = false,
            Child = panel
        };
    }

    // Les cartes masquees (zero hors « Aujourd'hui », journee a cloturer sans retard)
    // sortent de leur bande apres projection : c'est le seul moment ou on le sait. Le
    // chrome de la bande - compteur de cartes, « suivi seulement », etat vide - est donc
    // recalcule ici et pas au squelette, sinon une bande videe de ses zeros garderait un
    // compteur qui ment et n'afficherait jamais son etat vide.
    private void RefreshBands(HomeLayout layout)
    {
        foreach (var collection in new[] { overdueCards, todayCards, watchCards })
        {
            foreach (var hidden in collection.Where(card => card.IsHidden).ToList())
            {
                collection.Remove(hidden);
            }
        }

        RenderBand(OverdueBandPanel, HomeBand.Overdue, "En retard", layout);
        RenderBand(TodayBandPanel, HomeBand.Today, "Aujourd'hui", layout);
        RenderBand(WatchBandPanel, HomeBand.Watch, "À surveiller", layout);
    }

    // ================================ Chargements ================================

    private async Task FetchAsync(HomeSource source, HomeSourceResults results)
    {
        var client = context!.ApiClient;
        var url = context.ApiBaseUrl;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var unit = stationUnitCode ?? string.Empty;

        switch (source)
        {
            case HomeSource.BusinessDate:
                results.BusinessDate = await client.GetBusinessDateAsync(url, unit);
                break;
            case HomeSource.PendingApprovals:
                results.PendingApprovals = await client.GetPendingApprovalInstancesAsync(url);
                break;
            case HomeSource.FrontDesk:
                results.FrontDesk = await client.GetFrontDeskAsync(url, unit, today);
                break;
            case HomeSource.ArrivalBoard:
                results.ArrivalBoard = await client.GetArrivalsAsync(url, unit, today);
                break;
            case HomeSource.DepartureBoard:
                results.DepartureBoard = await client.GetDeparturesAsync(url, unit, today);
                break;
            case HomeSource.HousekeepingBoard:
                results.HousekeepingBoard = await client.GetHousekeepingBoardAsync(url, unit, today);
                break;
            case HomeSource.RevenueSummary:
                results.RevenueSummary = await client.GetDailyRevenueSummaryAsync(url, yesterday, today, stationUnitCode);
                break;
            case HomeSource.ReceiptsDraft:
                results.ReceiptsDraft = await client.GetCashReceiptSummaryAsync(url, today, today, stationUnitCode, ReceiptStatus.Draft);
                break;
            case HomeSource.ReceiptsConfirmed:
                results.ReceiptsConfirmed = await client.GetCashReceiptSummaryAsync(url, today, today, stationUnitCode, ReceiptStatus.Confirmed);
                break;
            case HomeSource.LowStock:
                results.LowStock = await client.GetLowStockAsync(url);
                break;
            case HomeSource.PaymentOrdersApproved:
                results.PaymentOrdersApproved = await client.GetPaymentOrdersAsync(url, null, null, null, PaymentOrderStatus.Approved);
                break;
            case HomeSource.PurchaseOrdersDraft:
                results.PurchaseOrdersDraft = await client.GetPurchaseOrdersAsync(url, null, null, null, null, nameof(PurchaseOrderStatus.Draft));
                break;
            case HomeSource.PurchaseOrdersApproved:
                results.PurchaseOrdersApproved = await client.GetPurchaseOrdersAsync(url, null, null, null, null, nameof(PurchaseOrderStatus.Approved));
                break;
            case HomeSource.InventoryCountsDraft:
                results.InventoryCountsDraft = await client.GetInventoryCountsAsync(url, null, InventoryCountStatus.Draft);
                break;
            case HomeSource.AbsencesRequested:
                results.AbsencesRequested = await client.GetHrAbsencesAsync(url, null, AbsenceStatus.Requested);
                break;
            case HomeSource.PayrollPeriods:
                results.PayrollPeriods = await client.GetPayrollPeriodsAsync(url);
                break;
            case HomeSource.EventsToday:
                results.EventsToday = await client.GetEventsAsync(url, unit, today, today);
                break;
            case HomeSource.HaccpReadings:
                results.HaccpReadings = await client.GetTemperatureReadingsAsync(
                    url,
                    new DateTimeOffset(DateTime.Today, TimeSpan.Zero),
                    new DateTimeOffset(DateTime.Today.AddDays(1), TimeSpan.Zero),
                    null,
                    nonCompliantOnly: true);
                break;
            case HomeSource.BackupStatus:
                results.BackupStatus = await client.GetBackupStatusAsync(url);
                break;
            case HomeSource.Workstations:
                results.Workstations = await client.GetWorkstationsAsync(url);
                break;
            case HomeSource.UnitDashboardYesterday:
                results.UnitDashboardYesterday = await client.GetUnitDashboardAsync(url, yesterday);
                break;
            case HomeSource.Aging:
                results.Aging = await client.GetAgingBalanceAsync(url, null, null);
                break;
            case HomeSource.DecCockpit:
                results.DecCockpit = await client.GetDecCockpitAsync(url, today);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Source d'accueil inconnue.");
        }
    }

    // Seules les cartes de CETTE source changent d'etat : les autres restent ou elles en
    // sont, chargees ou en attente.
    private void ProjectSource(HomeSource source, Dictionary<string, HomeCardModel> cards, HomeSourceResults results)
    {
        foreach (var card in cards.Values.Where(card => card.Slot.Queue.Source == source))
        {
            card.Card = HomeProjection.Project(card.Slot, results, currencyLabel);
        }

        if (source == HomeSource.BusinessDate)
        {
            RefreshBusinessDate(results);
        }
    }

    // ================================== Bandeau ==================================

    private void RefreshBanner(HomeLayout layout)
    {
        RefreshDateLine();

        ProfileButton.IsEnabled = layout.CanOpenSettings;
        PreferencesButton.IsEnabled = layout.CanOpenSettings;
        ProfileButton.ToolTip = layout.CanOpenSettings
            ? "Votre compte, vos rôles et vos permissions, dans Paramétrage global"
            : ModuleTile.AccessDeniedToolTip;
        PreferencesButton.ToolTip = layout.CanOpenSettings
            ? "Apparence, densité et réglages de ce poste"
            : ModuleTile.AccessDeniedToolTip;

        // Ligne « Unité du poste » : elle n'a de sens que si au moins une file unitaire
        // est LISIBLE. Un poste RH n'a rien a regler, donc rien a lire.
        StationUnitPanel.Visibility = layout.ShowUnitLine ? Visibility.Visible : Visibility.Collapsed;
        StationUnitTextBlock.Text = stationUnitCode is null
            ? "Unité du poste : — non définie"
            : $"Unité du poste : {stationUnitCode}";
        ChangeStationUnitButton.Content = stationUnitCode is null ? "Définir" : "Changer";

        UnitMissingBanner.Visibility = layout.ShowUnitMissingBanner ? Visibility.Visible : Visibility.Collapsed;

        BusinessDatePanel.Visibility = layout.ShowBusinessDate ? Visibility.Visible : Visibility.Collapsed;

        if (layout.ShowBusinessDate)
        {
            BusinessDateTextBlock.Text = $"Date métier {stationUnitCode} : —";
            SetBusinessDatePill("chargement", validated: false);
        }
    }

    // Nom de l'etablissement et libelle de devise : un seul appel, une seule fois par
    // session. Sans settings.read la ligne est simplement absente - l'utilisateur n'a
    // rien a y faire, donc rien a lui dire.
    private async Task RefreshEstablishmentAsync(HomeLayout layout)
    {
        if (context is null || !layout.ShowEstablishment || EstablishmentTextBlock.Visibility == Visibility.Visible)
        {
            return;
        }

        await context.RunAsync(async () =>
        {
            var settings = await context.ApiClient.GetApplicationSettingsAsync(context.ApiBaseUrl);

            currencyLabel = settings.CurrencyLabel;

            if (!string.IsNullOrWhiteSpace(settings.CompanyName))
            {
                EstablishmentTextBlock.Text = settings.CompanyName;
                EstablishmentTextBlock.Visibility = Visibility.Visible;
            }
        });
    }

    private void RefreshDateLine()
    {
        DateTextBlock.Text = DateTime.Today.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
    }

    // La date metier vient du serveur, pastille comprise : le client relaie IsLate et
    // PendingDays, il ne compare aucune date lui-meme.
    private void RefreshBusinessDate(HomeSourceResults results)
    {
        if (results.Failed.Contains(HomeSource.BusinessDate))
        {
            BusinessDateTextBlock.Text = $"Date métier {stationUnitCode} : — indisponible";
            SetBusinessDatePill("indisponible", validated: false);
            return;
        }

        if (results.BusinessDate is not { } date)
        {
            return;
        }

        BusinessDateTextBlock.Text =
            $"Date métier {stationUnitCode} : {date.BusinessDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}";

        SetBusinessDatePill(
            date.IsLate
                ? date.PendingDays == 1 ? "en retard · 1 jour" : $"en retard · {date.PendingDays} jours"
                : "à jour",
            validated: !date.IsLate);
    }

    private void SetBusinessDatePill(string text, bool validated)
    {
        BusinessDatePillText.Text = text;
        BusinessDatePill.Background = TryFindResource(validated ? "StatusValidatedBackgroundBrush" : "StatusSubmittedBackgroundBrush") as System.Windows.Media.Brush;
        BusinessDatePillText.Foreground = TryFindResource(validated ? "StatusValidatedForegroundBrush" : "StatusSubmittedForegroundBrush") as System.Windows.Media.Brush;
    }

    private void RefreshSummary(HomeLayout layout, Dictionary<string, HomeCardModel> cards)
    {
        var visible = cards.Values.Where(card => !card.IsHidden).ToList();

        var overdue = visible.Count(card => card.Band == HomeBand.Overdue);
        var today = visible.Count(card => card.Band == HomeBand.Today);
        var watch = visible.Count(card => card.Band == HomeBand.Watch);

        var summary = $"{overdue} en retard · {today} aujourd'hui · {watch} à surveiller";

        if (layout.WatchOnly)
        {
            summary += " · suivi seulement";
        }

        SummaryTextBlock.Text = summary;
    }

    private void RefreshFailedSourcesBanner(Dictionary<string, HomeCardModel> cards, HomeSourceResults results)
    {
        if (results.Failed.Count == 0)
        {
            FailedSourcesBanner.Visibility = Visibility.Collapsed;
            return;
        }

        // Les libelles nomment les ECRANS concernes, pas les routes : c'est ce que
        // l'utilisateur reconnait. Un message par source noierait le bandeau de session.
        var screens = cards.Values
            .Where(card => results.Failed.Contains(card.Slot.Queue.Source))
            .Select(card => card.TargetLabel)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.CurrentCulture)
            .ToList();

        var count = results.Failed.Count;

        FailedSourcesTextBlock.Text = screens.Count > 0
            ? $"{count} compteur{(count > 1 ? "s n'ont" : " n'a")} pas répondu ({string.Join(", ", screens)}). Les autres sont à jour."
            : $"{count} compteur{(count > 1 ? "s n'ont" : " n'a")} pas répondu. Les autres sont à jour.";

        FailedSourcesBanner.Visibility = Visibility.Visible;
    }

    private void RefreshFreshness()
    {
        FreshnessTextBlock.Text = lastLoadedUtc is null
            ? string.Empty
            : $"actualisé à {lastLoadedUtc.Value.ToLocalTime():HH:mm}";
    }

    // ============================ Derniers écrans ouverts ============================

    private void RefreshRecentScreens()
    {
        var recent = DesktopSettings.LoadRecentTabs()
            .Where(canOpenModule)
            .Select(BuildRecentScreen)
            .Where(screen => screen is not null)
            .ToList();

        RecentScreensItemsControl.ItemsSource = recent;
        RecentScreensPanel.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static HomeRecentScreen? BuildRecentScreen(int tabIndex)
    {
        if (!FunctionalArchitectureCatalog.TryGetPrimaryPath(tabIndex, out var path) || path is null)
        {
            return null;
        }

        return new HomeRecentScreen(
            tabIndex,
            $"{path.Screen.Label} · {path.Module.Label}",
            path.Domain.IconKey,
            string.Join(" › ", path.Labels),
            $"Ouvrir {path.Screen.Label}, {path.Module.Label}, {path.Domain.Label}");
    }

    // ============================== Carte produit ==============================

    private void RefreshProductCard()
    {
        var maturities = FunctionalArchitectureCatalog.Domains
            .GroupBy(domain => domain.Maturity)
            .ToDictionary(group => group.Key, group => group.Count());

        // « Fonctionnel : 11 » plutot que « 11 fonctionnels » : les quatre libelles sont
        // ceux de FunctionalMaturityMapper, source unique, et les accorder au pluriel
        // demanderait de les reecrire ici - donc de les dedoubler.
        var byMaturity = string.Join(" · ", Enum.GetValues<FunctionalMaturity>()
            .Select(maturity => $"{FunctionalMaturityMapper.Label(maturity)} : {maturities.GetValueOrDefault(maturity)}"));

        ProductSummaryTextBlock.Text =
            $"{ModuleCatalog.CountOf(ModuleStatus.Disponible)} modules disponibles sur {ModuleCatalog.Entries.Count}  ·  domaines : {byMaturity}";
    }

    // ================================ Gestionnaires ================================

    private void WorkCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeCardModel { IsTargetLocked: false } card)
        {
            NavigateRequested?.Invoke(card.TargetTab);
        }
    }

    private void RecentScreen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeRecentScreen screen)
        {
            NavigateRequested?.Invoke(screen.TabIndex);
        }
    }

    private void Profile_Click(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(SettingsTab);

    private void Preferences_Click(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(SettingsTab);

    private void Security_Click(object sender, RoutedEventArgs e) => ChangePasswordRequested?.Invoke();

    private void OpenCatalog_Click(object sender, RoutedEventArgs e) => OpenCatalogRequested?.Invoke();

    private async void RefreshHome_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    // Le reglage d'unite du poste s'ecrit dans cet editeur, et nulle part ailleurs : le
    // bandeau ne porte AUCUN selecteur (piloter plusieurs unites est le role des onglets de
    // pilotage), seulement le rappel de ce que ce poste est.
    private async void ChangeStationUnit_Click(object sender, RoutedEventArgs e)
    {
        StationUnitEditor.Visibility = Visibility.Visible;

        // La liste des unites n'est lisible qu'avec units.read : sans elle, le code se
        // saisit. Chargee a l'ouverture de l'editeur seulement - l'accueil ne paie pas cet
        // appel a chaque connexion, et un poste de caisse ne le paie jamais.
        if (context is not null && !unitsLoaded && context.HasPermission(Domain.Identity.PermissionCatalog.UnitsRead))
        {
            unitsLoaded = true;

            await context.RunAsync(async () =>
            {
                var units = await context.ApiClient.GetHotelUnitsAsync(context.ApiBaseUrl, includeInactive: false);

                StationUnitComboBox.ItemsSource = units
                    .Select(unit => new HomeStationUnitOption(unit.Code, $"{unit.Code} · {unit.Name}"))
                    .ToList();

                StationUnitComboBox.Visibility = Visibility.Visible;
            });
        }

        if (StationUnitComboBox.Visibility == Visibility.Visible)
        {
            StationUnitComboBox.SelectedValue = stationUnitCode;
            StationUnitComboBox.Focus();
        }
        else
        {
            StationUnitTextBox.Text = stationUnitCode ?? string.Empty;
            StationUnitTextBox.Visibility = Visibility.Visible;
            StationUnitTextBox.Focus();
        }
    }

    private async void SaveStationUnit_Click(object sender, RoutedEventArgs e)
    {
        var chosen = StationUnitComboBox.Visibility == Visibility.Visible
            ? StationUnitComboBox.SelectedValue as string
            : StationUnitTextBox.Text;

        if (string.IsNullOrWhiteSpace(chosen))
        {
            context?.SetStatus("Choisissez une unité, ou retirez l'unité de ce poste.", true);
            return;
        }

        DesktopSettings.SaveStationUnitCode(chosen);
        StationUnitEditor.Visibility = Visibility.Collapsed;

        // Le serveur valide le code au premier appel : un code faux ne donne pas des
        // chiffres faux, il donne des cartes « Indisponible » avec SON message.
        context?.SetStatus($"Ce poste est rattaché à l'unité {chosen.Trim().ToUpperInvariant()}.");
        await LoadAsync();
    }

    private async void ClearStationUnit_Click(object sender, RoutedEventArgs e)
    {
        DesktopSettings.SaveStationUnitCode(null);
        StationUnitEditor.Visibility = Visibility.Collapsed;
        context?.SetStatus("Ce poste n'est plus rattaché à une unité.");
        await LoadAsync();
    }

    private void CancelStationUnit_Click(object sender, RoutedEventArgs e)
    {
        StationUnitEditor.Visibility = Visibility.Collapsed;
    }

    // ================================== Libelles ==================================

    private static string FileCountLabel(int count) => count == 1 ? "1 file" : $"{count} files";

    // Le perimetre que la pastille annonce. « Groupe · toutes unités » n'est pas une
    // formule de style : aucune affectation utilisateur-unite n'existe cote serveur, donc
    // le cockpit DEC est groupe-entier meme pour un directeur d'unite, et la carte le dit.
    private string ScopeLabel(HomeSlot slot) => slot.Scope switch
    {
        HomeScope.Unit => stationUnitCode ?? "Unité du poste",
        HomeScope.Group => "Groupe · toutes unités",
        HomeScope.Me => "Ma décision",
        _ => "Système"
    };

    private static string TargetLabel(int tabIndex) =>
        FunctionalArchitectureCatalog.TryGetPrimaryPath(tabIndex, out var path) && path is not null
            ? path.Screen.Label
            : "Écran";

    private static string IconKeyFor(int tabIndex) =>
        FunctionalArchitectureCatalog.TryGetPrimaryPath(tabIndex, out var path) && path is not null
            ? path.Domain.IconKey
            : string.Empty;
}

/// <summary>Une puce de « derniers écrans ouverts ».</summary>
public sealed record HomeRecentScreen(int TabIndex, string Label, string IconKey, string ToolTip, string AccessibleName);

/// <summary>Une unite proposee dans l'editeur d'unite du poste.</summary>
public sealed record HomeStationUnitOption(string Code, string Label);
