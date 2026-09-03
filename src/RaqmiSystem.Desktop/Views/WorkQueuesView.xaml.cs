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

    // Jeton de session : incremente par OpenSession et par ResetState. Un chargement le capture
    // au depart et abandonne des qu'il a change - sinon une deconnexion survenue entre deux
    // appels laisserait la boucle en vol repeindre l'ecran deconnecte avec la synthese, les
    // etats vides et les ECRANS LISIBLES du profil qui vient de partir.
    private int sessionToken;

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

        sessionToken++;

        GreetingTextBlock.Text = $"Bonjour, {user.DisplayName}";
        RefreshDateLine();
    }

    /// <summary>
    /// Le reglage d'unite du poste a change ailleurs (Parametrage global) : la prochaine venue
    /// sur l'onglet 0 recharge, sans attendre les cinq minutes de fraicheur.
    /// </summary>
    public void Invalidate() => lastLoadedUtc = null;

    public async Task LoadAsync()
    {
        if (context is null || !context.ApiClient.IsAuthenticated || isLoading)
        {
            return;
        }

        var token = sessionToken;

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
            await RefreshEstablishmentAsync(layout, token);

            // Une RunAsync PAR source, de la plus legere a la plus lourde (l'ordre de
            // l'enumeration) : les cartes se remplissent au fil des reponses. Entre deux
            // appels le thread d'interface est libre : le jeton est donc revu AVANT et APRES
            // chaque attente, et aucune ecriture d'interface ne suit une deconnexion.
            foreach (var source in layout.Sources)
            {
                if (token != sessionToken)
                {
                    return;
                }

                var ok = false;

                await context.RunAsync(async () =>
                {
                    await FetchAsync(source, results);
                    ok = true;                       // pose seulement si l'appel a abouti
                });

                if (token != sessionToken)
                {
                    return;
                }

                if (!ok)
                {
                    results.Failed.Add(source);      // RunAsync a deja affiche l'erreur
                }

                ProjectSource(source, cards, results);
            }

            if (token != sessionToken)
            {
                return;
            }

            RefreshFailedSourcesBanner(cards, results);
            RefreshSummary(layout, cards);
            RefreshBands(layout, cards);

            lastLoadedUtc = DateTimeOffset.UtcNow;
            RefreshFreshness();
        }
        finally
        {
            // Une session a pu s'ouvrir pendant que ce chargement-ci se terminait : c'est
            // ResetState qui a deja rendu la main, et rendre isLoading a faux ici ecraserait
            // le chargement du NOUVEAU profil.
            if (token == sessionToken)
            {
                isLoading = false;
            }
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
        // Le jeton bouge D'ABORD : un chargement encore en vol doit se voir perime des sa
        // prochaine reprise, et la session suivante doit pouvoir charger sans attendre la fin
        // de celui-la (d'ou isLoading remis a faux ici, et plus dans son propre finally).
        sessionToken++;
        isLoading = false;

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
        FailedSourcesBanner.Visibility = Visibility.Collapsed;
        RecentScreensPanel.Visibility = Visibility.Collapsed;
        FreshnessTextBlock.Text = string.Empty;

        currencyLabel = null;
        lastLoadedUtc = null;
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
            cards[slot.Queue.Id] = new HomeCardModel(slot, ScopeLabel(slot), TargetLabel(slot.TargetTab))
            {
                IconKey = IconKeyFor(slot.TargetTab)
            };
        }

        PlaceCards(cards);
        RenderBands(layout);

        return cards;
    }

    private ObservableCollection<HomeCardModel> CollectionFor(HomeBand band) => band switch
    {
        HomeBand.Overdue => overdueCards,
        HomeBand.Today => todayCards,
        _ => watchCards
    };

    // Repartition des cartes dans les trois bandes, TOUJOURS par la bande que la carte porte :
    // celle du registre tant que rien n'a repondu, celle qu'un booleen serveur impose ensuite.
    // Une sauvegarde a l'heure quitte ainsi « En retard » pour « À surveiller », et l'en-tete,
    // le compteur et la synthese comptent enfin la meme chose.
    private void PlaceCards(Dictionary<string, HomeCardModel> cards)
    {
        var projected = cards.Values.Select(model => model.Card).ToList();

        foreach (var band in new[] { HomeBand.Overdue, HomeBand.Today, HomeBand.Watch })
        {
            var collection = CollectionFor(band);
            collection.Clear();

            foreach (var card in HomeBandPlacement.InBand(projected, band))
            {
                collection.Add(cards[card.Slot.Queue.Id]);
            }
        }
    }

    private void RenderBands(HomeLayout layout)
    {
        RenderBand(OverdueBandPanel, HomeBand.Overdue, "En retard", layout);
        RenderBand(TodayBandPanel, HomeBand.Today, "Aujourd'hui", layout);
        RenderBand(WatchBandPanel, HomeBand.Watch, "À surveiller", layout);
    }

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
                        "Vos files dépendent d'une unité : fixez l'unité de ce poste dans Paramétrage global › Poste de travail."));
                    break;

                case HomeEmptyReason.NoQueues when band == HomeBand.Today:
                    host.Children.Add(BuildEmptyBox(
                        "Rien à traiter",
                        "Aucune file de travail n'est ouverte à votre profil. Vos écrans restent accessibles par la barre latérale et le catalogue."));
                    break;

                case HomeEmptyReason.UnitMissing:
                case HomeEmptyReason.NoQueues:
                    // En retard et À surveiller se contentent d'une ligne : trois boites
                    // d'etat vide sur une page en feraient un mur de vide. L'attenuation
                    // passe par les TOKENS - le titre reste en TextMuted pleine opacite, seule
                    // l'explication est en CaptionText. Aucune Opacity sur un element portant
                    // du texte : a 0,7 le titre tomberait a ~2,9:1, sous le seuil WCAG AA, et
                    // c'est l'etat que le profil RH voit a chaque ouverture de session.
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

    // Apres projection seulement on sait DEUX choses : quelles cartes sont masquees (zero hors
    // « Aujourd'hui », journee a cloturer sans retard) et dans quelle bande le serveur a mis
    // chacune. Les trois collections sont donc entierement refaites ici, et le chrome de la
    // bande - compteur de cartes, « suivi seulement », etat vide - recalcule avec elles :
    // sinon une bande garderait un compteur qui ment, n'afficherait jamais son etat vide, et
    // une carte deplacee par le serveur resterait sous l'en-tete du registre.
    private void RefreshBands(HomeLayout layout, Dictionary<string, HomeCardModel> cards)
    {
        PlaceCards(cards);
        RenderBands(layout);
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
                // Bornes en heure LOCALE du poste : forcer l'offset a zero leve
                // ArgumentException hors UTC, et cette exception-la fermerait l'application.
                results.HaccpReadings = await client.GetTemperatureReadingsAsync(
                    url,
                    HomeDayWindow.Start(DateTime.Today),
                    HomeDayWindow.End(DateTime.Today),
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
            ? "Apparence, densité et unité de ce poste, dans Paramétrage global"
            : ModuleTile.AccessDeniedToolTip;

        // Ligne « Unité du poste » : elle n'a de sens que si au moins une file unitaire
        // est LISIBLE. Un poste RH n'a rien a regler, donc rien a lire.
        StationUnitPanel.Visibility = layout.ShowUnitLine ? Visibility.Visible : Visibility.Collapsed;
        StationUnitTextBlock.Text = stationUnitCode is null
            ? "Unité du poste : — non définie"
            : $"Unité du poste : {stationUnitCode}";
        ChangeStationUnitButton.Content = stationUnitCode is null ? "Définir" : "Changer";

        // Le reglage s'ecrit dans « Paramétrage global › Poste de travail », et nulle part
        // ailleurs : un bouton qui n'y mene pas serait une promesse creuse, et un second
        // endroit qui ecrit DesktopSettings serait la faute meme pour laquelle Apparence et
        // Densite ont ete refusees sur cet ecran.
        ChangeStationUnitButton.Visibility = layout.CanOpenSettings ? Visibility.Visible : Visibility.Collapsed;
        OpenStationSettingsButton.Visibility = layout.CanOpenSettings ? Visibility.Visible : Visibility.Collapsed;

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
    private async Task RefreshEstablishmentAsync(HomeLayout layout, int token)
    {
        if (context is null || !layout.ShowEstablishment || EstablishmentTextBlock.Visibility == Visibility.Visible)
        {
            return;
        }

        await context.RunAsync(async () =>
        {
            var settings = await context.ApiClient.GetApplicationSettingsAsync(context.ApiBaseUrl);

            // La deconnexion a pu tomber pendant cet appel : ResetState vient de masquer la
            // ligne, la reafficher la ferait revenir sur un ecran deconnecte.
            if (token != sessionToken)
            {
                return;
            }

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

    // L'unite du poste se REGLE dans « Paramétrage global › Poste de travail » : un seul
    // endroit ecrit DesktopSettings, et le bandeau ne porte que le rappel de ce que ce poste
    // est. Piloter plusieurs unites est le role des onglets 3, 19 et 20, pas de l'accueil.
    private void ChangeStationUnit_Click(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(SettingsTab);

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
