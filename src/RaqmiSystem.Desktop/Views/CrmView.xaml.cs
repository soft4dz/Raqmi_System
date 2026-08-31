using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module CRM &amp; experience client : vue client 360, segmentation du fichier
/// clients, programme de fidelite, campagnes, satisfaction (NPS) et journal des
/// contacts. Vue autonome : elle ne connait que le ModuleViewContext que la fenetre
/// lui prete, jamais MainWindow ni une autre vue.
///
/// Deux droits distincts gouvernent l'ecran : crm.write pour tout ce qui qualifie le
/// client et pilote les campagnes, crm.loyalty pour les seuls mouvements de points.
/// Les boutons sont grises selon le droit qui les concerne, plutot que de laisser
/// l'utilisateur decouvrir un 403 apres avoir saisi tout un formulaire ; le refus
/// fait evidemment autorite cote serveur.
/// </summary>
public partial class CrmView : UserControl
{
    private const string WritePermissionHint =
        "Permission crm.write requise : votre profil ne peut que consulter la relation client.";

    private const string LoyaltyPermissionHint =
        "Permission crm.loyalty requise : votre profil ne peut pas mouvementer les points de fidélité.";

    // Libelles francais des enums du domaine : seul l'affichage est traduit, la
    // valeur envoyee a l'API reste celle du domaine.
    private static readonly EnumOption<LoyaltyTransactionKind>[] MovementKinds =
    [
        new(LoyaltyTransactionKind.Earn, "Créditer"),
        new(LoyaltyTransactionKind.Redeem, "Débiter"),
        new(LoyaltyTransactionKind.Adjustment, "Corriger"),
        new(LoyaltyTransactionKind.Expiry, "Expirer")
    ];

    private static readonly EnumOption<CampaignChannel>[] CampaignChannels =
    [
        new(CampaignChannel.Email, "E-mail"),
        new(CampaignChannel.Sms, "SMS"),
        new(CampaignChannel.Phone, "Téléphone"),
        new(CampaignChannel.OnSite, "Sur place")
    ];

    private static readonly EnumOption<CampaignStatus>[] CampaignStatuses =
    [
        new(CampaignStatus.Draft, "Brouillon"),
        new(CampaignStatus.Scheduled, "Planifiée"),
        new(CampaignStatus.Running, "En cours"),
        new(CampaignStatus.Completed, "Terminée"),
        new(CampaignStatus.Cancelled, "Annulée")
    ];

    private static readonly EnumOption<SatisfactionSource>[] SatisfactionSources =
    [
        new(SatisfactionSource.FrontDesk, "Réception"),
        new(SatisfactionSource.InRoom, "En chambre"),
        new(SatisfactionSource.Email, "Courriel"),
        new(SatisfactionSource.Online, "En ligne"),
        new(SatisfactionSource.Phone, "Téléphone")
    ];

    private static readonly EnumOption<InteractionChannel>[] InteractionChannels =
    [
        new(InteractionChannel.Phone, "Téléphone"),
        new(InteractionChannel.Email, "Courriel"),
        new(InteractionChannel.Sms, "SMS"),
        new(InteractionChannel.InPerson, "En personne"),
        new(InteractionChannel.Web, "Web")
    ];

    private static readonly EnumOption<InteractionDirection>[] InteractionDirections =
    [
        new(InteractionDirection.Inbound, "Entrant"),
        new(InteractionDirection.Outbound, "Sortant")
    ];

    private static readonly EnumOption<NpsCategory>[] NpsCategories =
    [
        new(NpsCategory.Promoter, "Promoteur"),
        new(NpsCategory.Passive, "Passif"),
        new(NpsCategory.Detractor, "Détracteur")
    ];

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution par le message de permission : l'affectation doit rester
    // symetrique (voir ApplyPermissionHint).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    private bool canWriteCrm = true;

    private bool canMoveLoyaltyPoints = true;

    // Onglets deja charges depuis l'ouverture de la session : chaque sous-onglet
    // charge ses donnees a sa premiere ouverture, pas au chargement du module.
    private readonly HashSet<int> loadedTabs = [];

    // Client dont la vue 360 est affichee, null tant qu'aucune fiche n'est ouverte.
    private string? openCustomerCode;

    // Null en mode creation, code de l'element edite en mode modification.
    private string? editingSegmentCode;

    private string? editingTierCode;

    private string? editingCampaignCode;

    // Compte de fidelite charge dans l'onglet Fidelite.
    private string? loyaltyAccountCode;

    public CrmView()
    {
        InitializeComponent();

        MovementKindComboBox.ItemsSource = MovementKinds;
        MovementKindComboBox.SelectedIndex = 0;
        CampaignChannelComboBox.ItemsSource = CampaignChannels;
        CampaignChannelComboBox.SelectedIndex = 0;
        SurveySourceComboBox.ItemsSource = SatisfactionSources;
        SurveySourceComboBox.SelectedIndex = 0;
        InteractionChannelComboBox.ItemsSource = InteractionChannels;
        InteractionChannelComboBox.SelectedIndex = 0;
        InteractionDirectionComboBox.ItemsSource = InteractionDirections;
        InteractionDirectionComboBox.SelectedIndex = 0;

        // Le filtre de statut porte une entree « Tous » en tete, contrairement au
        // choix de canal d'une campagne, qui est obligatoire.
        CampaignStatusFilterComboBox.ItemsSource = new[] { new EnumOption<CampaignStatus>(null, "Tous les statuts") }
            .Concat(CampaignStatuses.Select(option => new EnumOption<CampaignStatus>(option.Value, option.Label)))
            .ToArray();
        CampaignStatusFilterComboBox.SelectedIndex = 0;

        ResetSegmentForm();
        ResetTierForm();
        ResetCampaignForm();
        ApplyDefaultDates();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canWriteCrm = context.HasPermission(PermissionCatalog.CrmWrite);
        canMoveLoyaltyPoints = context.HasPermission(PermissionCatalog.CrmLoyalty);

        loadedTabs.Clear();
        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge le module. Appelee a la premiere ouverture de l'onglet : seules les
    /// donnees communes (unites, segments) et l'onglet visible sont chargees, les
    /// autres le seront a leur premiere ouverture. Sort silencieusement tant qu'aucun
    /// contexte n'est disponible ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadReferenceDataAsync();
            await LoadCurrentTabAsync();
        });
    }

    /// <summary>Vide les grilles et les formulaires (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        loadedTabs.Clear();
        openCustomerCode = null;
        loyaltyAccountCode = null;

        GuestsDataGrid.ItemsSource = null;
        SegmentsDataGrid.ItemsSource = null;
        TiersDataGrid.ItemsSource = null;
        MovementsDataGrid.ItemsSource = null;
        CampaignsDataGrid.ItemsSource = null;
        AudienceDataGrid.ItemsSource = null;
        SurveysDataGrid.ItemsSource = null;
        NpsUnitsDataGrid.ItemsSource = null;
        InteractionsDataGrid.ItemsSource = null;
        Customer360InteractionsDataGrid.ItemsSource = null;
        Customer360SurveysDataGrid.ItemsSource = null;
        Customer360CampaignsDataGrid.ItemsSource = null;

        Customer360Panel.Visibility = Visibility.Collapsed;
        GuestSearchTextBox.Text = string.Empty;
        Customer360CodeTextBox.Text = string.Empty;
        LoyaltyCustomerCodeTextBox.Text = string.Empty;
        LoyaltyAccountTitleTextBlock.Text = "Aucun compte chargé";
        LoyaltyAccountDetailTextBlock.Text = "Saisissez un code client pour voir son solde, son palier et ses mouvements.";
        AudienceSummaryTextBlock.Text = "Sélectionnez une campagne puis calculez son audience.";

        ResetSegmentForm();
        ResetTierForm();
        ResetCampaignForm();
        UpdateActionButtons();
    }

    // ================================ Chargements ================================

    // Les unites et les segments alimentent plusieurs onglets : ils sont charges une
    // fois par ouverture du module, pas a chaque changement de sous-onglet.
    private async Task LoadReferenceDataAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var units = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        var unitOptions = units
            .OrderBy(unit => unit.DisplayOrder)
            .Select(unit => new CodeOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToArray();

        // Le filtre NPS accepte « toutes les unites » ; les formulaires de saisie
        // exigent une unite precise.
        NpsUnitComboBox.ItemsSource = new[] { new CodeOption(null, "Toutes les unités") }.Concat(unitOptions).ToArray();
        NpsUnitComboBox.SelectedIndex = 0;

        SurveyUnitComboBox.ItemsSource = unitOptions;
        SurveyUnitComboBox.SelectedIndex = unitOptions.Length == 0 ? -1 : 0;

        InteractionUnitComboBox.ItemsSource = new[] { new CodeOption(null, "Aucune unité") }.Concat(unitOptions).ToArray();
        InteractionUnitComboBox.SelectedIndex = 0;

        await ReloadSegmentOptionsAsync();
    }

    private async Task ReloadSegmentOptionsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var segments = await moduleContext.ApiClient.GetCrmSegmentsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        var options = segments
            .Select(segment => new CodeOption(segment.Code, $"{segment.Code} — {segment.Label}"))
            .ToArray();

        // Le segment reste facultatif partout : un client non qualifie et une campagne
        // adressee a tout le fichier sont des situations normales.
        var previousFilter = SelectedCode(GuestSegmentFilterComboBox);
        var previousProfile = SelectedCode(ProfileSegmentComboBox);
        var previousCampaign = SelectedCode(CampaignSegmentComboBox);

        GuestSegmentFilterComboBox.ItemsSource = new[] { new CodeOption(null, "Tous les segments") }.Concat(options).ToArray();
        ProfileSegmentComboBox.ItemsSource = new[] { new CodeOption(null, "Sans segment") }.Concat(options).ToArray();
        CampaignSegmentComboBox.ItemsSource = new[] { new CodeOption(null, "Tout le fichier clients") }.Concat(options).ToArray();

        SelectCode(GuestSegmentFilterComboBox, previousFilter);
        SelectCode(ProfileSegmentComboBox, previousProfile);
        SelectCode(CampaignSegmentComboBox, previousCampaign);
    }

    private async void CrmTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged est un evenement routé : ignorer ceux qui remontent des
        // DataGrid/ComboBox internes.
        if (!ReferenceEquals(e.OriginalSource, CrmTabs))
        {
            return;
        }

        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(LoadCurrentTabAsync);
    }

    // Chargement paresseux : la premiere ouverture d'un sous-onglet declenche son
    // chargement, les suivantes non - le bouton « Actualiser » de chaque onglet reste
    // le chemin explicite pour recharger.
    private async Task LoadCurrentTabAsync()
    {
        var tabIndex = CrmTabs.SelectedIndex;

        if (!loadedTabs.Add(tabIndex))
        {
            return;
        }

        switch (tabIndex)
        {
            case 0:
                await ReloadGuestsAsync();
                break;
            case 1:
                await ReloadSegmentsAsync();
                break;
            case 2:
                await ReloadTiersAsync();
                break;
            case 3:
                await ReloadCampaignsAsync();
                break;
            case 4:
                await ReloadSatisfactionAsync();
                break;
            case 5:
                await ReloadInteractionsAsync();
                break;
            default:
                loadedTabs.Remove(tabIndex);
                break;
        }
    }

    // ================================== Vue 360 ==================================

    private async void GuestSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RunAsync(ReloadGuestsAsync);
    }

    private async void GuestFilters_Changed(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadGuestsAsync);
    }

    private async Task ReloadGuestsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var guests = await moduleContext.ApiClient.GetCrmGuestsAsync(
            moduleContext.ApiBaseUrl,
            ReadOptional(GuestSearchTextBox),
            SelectedCode(GuestSegmentFilterComboBox),
            GuestVipOnlyCheckBox.IsChecked == true);

        GuestsDataGrid.ItemsSource = guests
            .Select(guest => new GuestRowView(
                guest.CustomerCode,
                guest.CustomerName,
                guest.SegmentLabel,
                guest.LoyaltyPoints,
                guest.LoyaltyTierLabel))
            .ToArray();
    }

    private async void GuestsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GuestsDataGrid.SelectedItem is not GuestRowView selected)
        {
            return;
        }

        await RunAsync(() => LoadCustomer360Async(selected.CustomerCode));
    }

    private async void Customer360CodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await OpenTypedCustomerAsync();
    }

    private async void OpenCustomer360Button_Click(object sender, RoutedEventArgs e)
    {
        await OpenTypedCustomerAsync();
    }

    private async Task OpenTypedCustomerAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var code = ReadOptional(Customer360CodeTextBox);

        if (code is null)
        {
            moduleContext.SetStatus("Saisissez le code du client à ouvrir.", isError: true);
            return;
        }

        await RunAsync(() => LoadCustomer360Async(code));
    }

    private async Task LoadCustomer360Async(string customerCode)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        // La date du POSTE, pas celle du serveur : « en cours » doit vouloir dire
        // aujourd'hui pour l'utilisateur qui regarde l'ecran.
        var view = await moduleContext.ApiClient.GetCrmCustomer360Async(
            moduleContext.ApiBaseUrl,
            customerCode,
            DateOnly.FromDateTime(DateTime.Today));

        openCustomerCode = view.Customer.Code;
        Customer360Panel.Visibility = Visibility.Visible;

        Customer360NameTextBlock.Text = $"{view.Customer.Code} — {view.Customer.Name}";

        var identity = new List<string>();

        if (!string.IsNullOrWhiteSpace(view.Customer.City))
        {
            identity.Add(view.Customer.City);
        }

        if (!string.IsNullOrWhiteSpace(view.Customer.Phone))
        {
            identity.Add(view.Customer.Phone);
        }

        if (!string.IsNullOrWhiteSpace(view.Customer.Email))
        {
            identity.Add(view.Customer.Email);
        }

        identity.Add(view.Customer.IsActive ? "Client actif" : "Client désactivé");

        if (view.Profile?.IsVip == true)
        {
            identity.Add("VIP");
        }

        Customer360IdentityTextBlock.Text = string.Join("  ·  ", identity);

        FillProfileForm(view.Profile);
        FillCounters(view);

        Customer360InteractionsDataGrid.ItemsSource = view.RecentInteractions.Select(ToRowView).ToArray();
        Customer360SurveysDataGrid.ItemsSource = view.RecentSurveys.Select(ToRowView).ToArray();
        Customer360CampaignsDataGrid.ItemsSource = view.LiveCampaigns.Select(ToRowView).ToArray();

        UpdateActionButtons();
    }

    private void FillProfileForm(GuestProfileResponse? profile)
    {
        SelectCode(ProfileSegmentComboBox, profile?.SegmentCode);
        ProfileLanguageTextBox.Text = profile?.PreferredLanguage ?? string.Empty;
        ProfileBirthDatePicker.SelectedDate = profile?.BirthDate?.ToDateTime(TimeOnly.MinValue);
        ProfilePreferencesTextBox.Text = profile?.Preferences ?? string.Empty;
        ProfileNotesTextBox.Text = profile?.Notes ?? string.Empty;
        ProfileVipCheckBox.IsChecked = profile?.IsVip == true;

        // Le consentement se lit avec sa DATE : c'est elle qui fait preuve. Une fiche
        // sans horodatage est un client a qui la question n'a jamais ete posee, ce qui
        // n'est pas la meme chose qu'un refus enregistre.
        Customer360ConsentTextBlock.Text = profile switch
        {
            null => "Aucune fiche CRM : ce client n'est pas encore qualifié. Enregistrez la fiche pour la créer.",
            { MarketingConsentUpdatedAt: null } =>
                "Consentement marketing : jamais recueilli. Les campagnes e-mail et SMS ne l'atteindront pas.",
            { MarketingConsent: true } =>
                $"Consentement marketing accordé le {FormatLocal(profile.MarketingConsentUpdatedAt)}.",
            _ => $"Consentement marketing refusé le {FormatLocal(profile.MarketingConsentUpdatedAt)}. "
                 + "Les campagnes e-mail et SMS ne l'atteindront pas."
        };
    }

    private void FillCounters(Customer360Response view)
    {
        StayCountTextBlock.Text = view.Stays.StayCount.ToString(CultureInfo.CurrentCulture);
        NightCountTextBlock.Text = view.Stays.NightCount.ToString(CultureInfo.CurrentCulture);
        StayRevenueTextBlock.Text = FormatAmount(view.Stays.StayRevenue);
        InvoicedTextBlock.Text = FormatAmount(view.Billing.InvoicedInclVat);
        OutstandingTextBlock.Text = FormatAmount(view.Billing.OutstandingInclVat);
        LoyaltyPointsTextBlock.Text = view.Loyalty.Balance.ToString(CultureInfo.CurrentCulture);

        SatisfactionTextBlock.Text = view.Satisfaction.LastScore.HasValue
            ? view.Satisfaction.LastScore.Value.ToString(CultureInfo.CurrentCulture)
            : "—";

        var tier = view.Loyalty.TierLabel is null
            ? "Aucun palier atteint"
            : $"Palier {view.Loyalty.TierLabel}";

        var next = view.Loyalty.NextTierLabel is null
            ? "dernier palier du programme atteint"
            : $"{view.Loyalty.PointsToNextTier} point(s) pour atteindre {view.Loyalty.NextTierLabel}";

        LoyaltySummaryTextBlock.Text = $"Fidélité : {view.Loyalty.Balance} point(s) — {tier}, {next}.";

        var lastStay = view.Stays.LastDeparture.HasValue
            ? $"dernier départ le {view.Stays.LastDeparture.Value.ToString("d", CultureInfo.CurrentCulture)}"
            : "aucun séjour effectué";

        StaySummaryTextBlock.Text =
            $"Séjours : {lastStay}, {view.Stays.UpcomingCount} à venir, "
            + $"{view.Stays.CancelledCount} annulé(s), {view.Stays.NoShowCount} no-show. "
            + $"Satisfaction : {view.Satisfaction.AnswerCount} réponse(s), "
            + $"note moyenne {FormatDecimal(view.Satisfaction.AverageScore)}.";
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || openCustomerCode is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var request = new SaveGuestProfileRequest(
                SelectedCode(ProfileSegmentComboBox),
                ReadOptional(ProfileLanguageTextBox),
                ReadDate(ProfileBirthDatePicker),
                ReadOptional(ProfilePreferencesTextBox),
                ReadOptional(ProfileNotesTextBox),
                ProfileVipCheckBox.IsChecked == true);

            var saved = await moduleContext.ApiClient.SaveCrmGuestProfileAsync(
                moduleContext.ApiBaseUrl,
                openCustomerCode,
                request);

            moduleContext.SetStatus($"Fiche CRM de {saved.CustomerCode} enregistrée.");

            await ReloadGuestsAsync();
            await LoadCustomer360Async(openCustomerCode);
        });
    }

    private async void GrantConsentButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConsentAsync(consent: true);
    }

    private async void WithdrawConsentButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConsentAsync(consent: false);
    }

    private async Task SetConsentAsync(bool consent)
    {
        var moduleContext = context;

        if (moduleContext is null || openCustomerCode is null)
        {
            return;
        }

        // Acte engageant au sens de la loi 18-07 : la reponse enregistree est datee et
        // fait foi, la confirmation est donc explicite.
        var question = consent
            ? $"Enregistrer le consentement marketing de {openCustomerCode} ?\nIl sera daté d'aujourd'hui et autorisera les campagnes e-mail et SMS."
            : $"Enregistrer le refus de {openCustomerCode} ?\nIl sera daté d'aujourd'hui et l'exclura des campagnes e-mail et SMS.";

        if (!Confirm(question, consent ? "Consentement accordé" : "Consentement retiré"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetCrmMarketingConsentAsync(
                moduleContext.ApiBaseUrl,
                openCustomerCode,
                consent);

            moduleContext.SetStatus(consent
                ? $"Consentement marketing de {openCustomerCode} enregistré."
                : $"Refus de {openCustomerCode} enregistré.");

            await LoadCustomer360Async(openCustomerCode);
        });
    }

    // ================================== Segments ==================================

    private async void RefreshSegmentsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ReloadSegmentsAsync();
            context?.SetStatus("Segments actualisés.");
        });
    }

    private async void SegmentFilters_Changed(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadSegmentsAsync);
    }

    private async Task ReloadSegmentsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var segments = await moduleContext.ApiClient.GetCrmSegmentsAsync(
            moduleContext.ApiBaseUrl,
            IncludeInactiveSegmentsCheckBox.IsChecked == true);

        SegmentsDataGrid.ItemsSource = segments;
        UpdateActionButtons();
    }

    private void SegmentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (SegmentsDataGrid.SelectedItem is not CustomerSegmentResponse selected)
        {
            return;
        }

        // Selectionner une ligne bascule le formulaire en modification : le code
        // identifie le segment cote API, il n'est donc plus modifiable.
        editingSegmentCode = selected.Code;
        SegmentFormTitleTextBlock.Text = $"Modifier {selected.Code}";
        SegmentCodeTextBox.Text = selected.Code;
        SegmentCodeTextBox.IsEnabled = false;
        SegmentLabelTextBox.Text = selected.Label;
        SegmentDescriptionTextBox.Text = selected.Description ?? string.Empty;
        SaveSegmentButton.Content = "Enregistrer les modifications";
    }

    private void NewSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        ResetSegmentForm();
        SegmentsDataGrid.SelectedItem = null;
        UpdateActionButtons();
    }

    private void ResetSegmentForm()
    {
        editingSegmentCode = null;
        SegmentFormTitleTextBlock.Text = "Nouveau segment";
        SegmentCodeTextBox.Text = string.Empty;
        SegmentCodeTextBox.IsEnabled = true;
        SegmentLabelTextBox.Text = string.Empty;
        SegmentDescriptionTextBox.Text = string.Empty;
        SaveSegmentButton.Content = "Créer le segment";
    }

    private async void SaveSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var label = ReadOptional(SegmentLabelTextBox);

            if (label is null)
            {
                moduleContext.SetStatus("Le libellé du segment est requis.", isError: true);
                return;
            }

            var description = ReadOptional(SegmentDescriptionTextBox);

            if (editingSegmentCode is null)
            {
                var code = ReadOptional(SegmentCodeTextBox);

                if (code is null)
                {
                    moduleContext.SetStatus("Le code du segment est requis.", isError: true);
                    return;
                }

                var created = await moduleContext.ApiClient.CreateCrmSegmentAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateCustomerSegmentRequest(code, label, description));

                moduleContext.SetStatus($"Segment {created.Code} créé.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateCrmSegmentAsync(
                    moduleContext.ApiBaseUrl,
                    editingSegmentCode,
                    new UpdateCustomerSegmentRequest(label, description));

                moduleContext.SetStatus($"Segment {updated.Code} mis à jour.");
            }

            ResetSegmentForm();
            await ReloadSegmentsAsync();
            await ReloadSegmentOptionsAsync();
        });
    }

    private async void ActivateSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSegmentActiveAsync(isActive: true);
    }

    private async void DeactivateSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSegmentActiveAsync(isActive: false);
    }

    private async Task SetSegmentActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (SegmentsDataGrid.SelectedItem is not CustomerSegmentResponse selected)
        {
            moduleContext.SetStatus("Sélectionnez un segment.", isError: true);
            return;
        }

        var question = isActive
            ? $"Réactiver le segment {selected.Code} ?\nIl sera de nouveau proposé aux fiches clients et aux campagnes."
            : $"Désactiver le segment {selected.Code} ?\nLes {selected.GuestCount} client(s) qui le portent et les campagnes déjà lancées le conservent, mais il ne sera plus proposé.";

        if (!Confirm(question, isActive ? "Activer le segment" : "Désactiver le segment"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var changed = await moduleContext.ApiClient.SetCrmSegmentActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            moduleContext.SetStatus(isActive
                ? $"Segment {changed.Code} activé."
                : $"Segment {changed.Code} désactivé.");

            await ReloadSegmentsAsync();
            await ReloadSegmentOptionsAsync();
        });
    }

    // ================================== Fidelite ==================================

    private async Task ReloadTiersAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var tiers = await moduleContext.ApiClient.GetCrmLoyaltyTiersAsync(moduleContext.ApiBaseUrl, includeInactive: true);

        TiersDataGrid.ItemsSource = tiers;
        UpdateActionButtons();
    }

    private void TiersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (TiersDataGrid.SelectedItem is not LoyaltyTierResponse selected)
        {
            return;
        }

        editingTierCode = selected.Code;
        TierFormTitleTextBlock.Text = $"Modifier {selected.Code}";
        TierCodeTextBox.Text = selected.Code;
        TierCodeTextBox.IsEnabled = false;
        TierLabelTextBox.Text = selected.Label;
        TierThresholdTextBox.Text = selected.PointsThreshold.ToString(CultureInfo.CurrentCulture);
        TierBenefitsTextBox.Text = selected.Benefits ?? string.Empty;
        SaveTierButton.Content = "Enregistrer les modifications";
    }

    private void NewTierButton_Click(object sender, RoutedEventArgs e)
    {
        ResetTierForm();
        TiersDataGrid.SelectedItem = null;
        UpdateActionButtons();
    }

    private void ResetTierForm()
    {
        editingTierCode = null;
        TierFormTitleTextBlock.Text = "Nouveau palier";
        TierCodeTextBox.Text = string.Empty;
        TierCodeTextBox.IsEnabled = true;
        TierLabelTextBox.Text = string.Empty;
        TierThresholdTextBox.Text = string.Empty;
        TierBenefitsTextBox.Text = string.Empty;
        SaveTierButton.Content = "Créer le palier";
    }

    private async void SaveTierButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var label = ReadOptional(TierLabelTextBox);

            if (label is null)
            {
                moduleContext.SetStatus("Le libellé du palier est requis.", isError: true);
                return;
            }

            if (!TryReadInteger(TierThresholdTextBox, out var threshold) || threshold < 0)
            {
                moduleContext.SetStatus("Le seuil de points doit être un nombre entier positif ou nul.", isError: true);
                return;
            }

            var benefits = ReadOptional(TierBenefitsTextBox);

            if (editingTierCode is null)
            {
                var code = ReadOptional(TierCodeTextBox);

                if (code is null)
                {
                    moduleContext.SetStatus("Le code du palier est requis.", isError: true);
                    return;
                }

                var created = await moduleContext.ApiClient.CreateCrmLoyaltyTierAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateLoyaltyTierRequest(code, label, threshold, benefits));

                moduleContext.SetStatus($"Palier {created.Code} créé.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateCrmLoyaltyTierAsync(
                    moduleContext.ApiBaseUrl,
                    editingTierCode,
                    new UpdateLoyaltyTierRequest(label, threshold, benefits));

                moduleContext.SetStatus($"Palier {updated.Code} mis à jour.");
            }

            ResetTierForm();
            await ReloadTiersAsync();
        });
    }

    private async void ActivateTierButton_Click(object sender, RoutedEventArgs e)
    {
        await SetTierActiveAsync(isActive: true);
    }

    private async void DeactivateTierButton_Click(object sender, RoutedEventArgs e)
    {
        await SetTierActiveAsync(isActive: false);
    }

    private async Task SetTierActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (TiersDataGrid.SelectedItem is not LoyaltyTierResponse selected)
        {
            moduleContext.SetStatus("Sélectionnez un palier.", isError: true);
            return;
        }

        // Le palier d'un client etant deduit du solde, retirer un palier change ce que
        // les clients concernes affichent : la confirmation le dit.
        var question = isActive
            ? $"Réactiver le palier {selected.Code} ?\nLes clients dont le solde atteint {selected.PointsThreshold} point(s) l'afficheront de nouveau."
            : $"Désactiver le palier {selected.Code} ?\nLes clients qui l'atteignaient afficheront le palier actif immédiatement inférieur.";

        if (!Confirm(question, isActive ? "Activer le palier" : "Désactiver le palier"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var changed = await moduleContext.ApiClient.SetCrmLoyaltyTierActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            moduleContext.SetStatus(isActive
                ? $"Palier {changed.Code} activé."
                : $"Palier {changed.Code} désactivé.");

            await ReloadTiersAsync();
        });
    }

    private async void LoyaltyCustomerCodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await LoadLoyaltyAccountAsync();
    }

    private async void LoadLoyaltyButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadLoyaltyAccountAsync();
    }

    private async Task LoadLoyaltyAccountAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var code = ReadOptional(LoyaltyCustomerCodeTextBox);

        if (code is null)
        {
            moduleContext.SetStatus("Saisissez le code du client.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var statement = await moduleContext.ApiClient.GetCrmLoyaltyStatementAsync(moduleContext.ApiBaseUrl, code);
            ShowLoyaltyStatement(statement);
        });
    }

    private void ShowLoyaltyStatement(LoyaltyStatementResponse statement)
    {
        loyaltyAccountCode = statement.CustomerCode;

        LoyaltyAccountTitleTextBlock.Text =
            $"{statement.CustomerCode} — {statement.CustomerName} : {statement.Balance} point(s)";

        var tier = statement.TierLabel is null
            ? "aucun palier atteint"
            : $"palier {statement.TierLabel}";

        var next = statement.NextTierLabel is null
            ? "dernier palier du programme atteint"
            : $"{statement.PointsToNextTier} point(s) pour atteindre {statement.NextTierLabel}";

        var benefits = string.IsNullOrWhiteSpace(statement.TierBenefits)
            ? string.Empty
            : $" Avantages : {statement.TierBenefits}";

        LoyaltyAccountDetailTextBlock.Text = $"Actuellement {tier}, {next}.{benefits}";

        MovementsDataGrid.ItemsSource = statement.Movements
            .Select(movement => new MovementRowView(
                movement.OccurredOn,
                DescribeMovementKind(movement.Kind),
                movement.Points,
                movement.Reason,
                movement.Reference,
                movement.CreatedBy))
            .ToArray();

        UpdateActionButtons();
    }

    private async void RecordMovementButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (loyaltyAccountCode is null)
        {
            moduleContext.SetStatus("Chargez d'abord le compte d'un client.", isError: true);
            return;
        }

        if (MovementKindComboBox.SelectedItem is not EnumOption<LoyaltyTransactionKind> { Value: { } kind })
        {
            moduleContext.SetStatus("Sélectionnez un type de mouvement.", isError: true);
            return;
        }

        if (!TryReadInteger(MovementPointsTextBox, out var points))
        {
            moduleContext.SetStatus("Le nombre de points doit être un nombre entier.", isError: true);
            return;
        }

        // Le sens vient du type de mouvement : la quantite saisie reste positive, sauf
        // pour une correction, seul mouvement qui va reellement dans les deux sens.
        if (kind == LoyaltyTransactionKind.Adjustment)
        {
            if (points == 0)
            {
                moduleContext.SetStatus("Une correction de zéro point ne déplacerait rien.", isError: true);
                return;
            }
        }
        else if (points <= 0)
        {
            moduleContext.SetStatus("Le nombre de points doit être strictement positif.", isError: true);
            return;
        }

        var reason = ReadOptional(MovementReasonTextBox);

        if (reason is null)
        {
            moduleContext.SetStatus("Le motif du mouvement est obligatoire.", isError: true);
            return;
        }

        var occurredOn = ReadDate(MovementDatePicker) ?? DateOnly.FromDateTime(DateTime.Today);
        var accountCode = loyaltyAccountCode;

        await moduleContext.RunAsync(async () =>
        {
            var statement = await moduleContext.ApiClient.RecordCrmLoyaltyMovementAsync(
                moduleContext.ApiBaseUrl,
                accountCode,
                kind,
                new LoyaltyMovementRequest(points, occurredOn, reason, ReadOptional(MovementReferenceTextBox)));

            ShowLoyaltyStatement(statement);

            MovementPointsTextBox.Text = string.Empty;
            MovementReasonTextBox.Text = string.Empty;
            MovementReferenceTextBox.Text = string.Empty;

            moduleContext.SetStatus($"Mouvement enregistré : {accountCode} totalise {statement.Balance} point(s).");
        });
    }

    // ================================== Campagnes ==================================

    private async void RefreshCampaignsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ReloadCampaignsAsync();
            context?.SetStatus("Campagnes actualisées.");
        });
    }

    private async void CampaignFilters_Changed(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadCampaignsAsync);
    }

    private async Task ReloadCampaignsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var from = ReadDate(CampaignFromDatePicker);
        var to = ReadDate(CampaignToDatePicker);

        if (from.HasValue && to.HasValue && from > to)
        {
            moduleContext.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var status = (CampaignStatusFilterComboBox.SelectedItem as EnumOption<CampaignStatus>)?.Value;

        var campaigns = await moduleContext.ApiClient.GetCrmCampaignsAsync(
            moduleContext.ApiBaseUrl,
            status,
            null,
            from,
            to);

        CampaignsDataGrid.ItemsSource = campaigns.Select(ToRowView).ToArray();
        UpdateActionButtons();
    }

    private void CampaignsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (CampaignsDataGrid.SelectedItem is not CampaignRowView selected)
        {
            return;
        }

        var campaign = selected.Source;

        editingCampaignCode = campaign.Code;
        CampaignFormTitleTextBlock.Text = $"Modifier {campaign.Code}";
        CampaignCodeTextBox.Text = campaign.Code;
        CampaignCodeTextBox.IsEnabled = false;
        CampaignLabelTextBox.Text = campaign.Label;
        CampaignChannelComboBox.SelectedItem = CampaignChannels.FirstOrDefault(option => option.Value == campaign.Channel);
        SelectCode(CampaignSegmentComboBox, campaign.TargetSegmentCode);
        CampaignStartDatePicker.SelectedDate = campaign.StartDate.ToDateTime(TimeOnly.MinValue);
        CampaignEndDatePicker.SelectedDate = campaign.EndDate.ToDateTime(TimeOnly.MinValue);
        CampaignObjectiveTextBox.Text = campaign.Objective ?? string.Empty;
        CampaignMessageTextBox.Text = campaign.Message ?? string.Empty;
        SaveCampaignButton.Content = "Enregistrer les modifications";
    }

    private void NewCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCampaignForm();
        CampaignsDataGrid.SelectedItem = null;
        UpdateActionButtons();
    }

    private void ResetCampaignForm()
    {
        editingCampaignCode = null;
        CampaignFormTitleTextBlock.Text = "Nouvelle campagne";
        CampaignCodeTextBox.Text = string.Empty;
        CampaignCodeTextBox.IsEnabled = true;
        CampaignLabelTextBox.Text = string.Empty;
        CampaignChannelComboBox.SelectedIndex = 0;
        CampaignObjectiveTextBox.Text = string.Empty;
        CampaignMessageTextBox.Text = string.Empty;
        CampaignCancelReasonTextBox.Text = string.Empty;

        if (CampaignSegmentComboBox.Items.Count > 0)
        {
            CampaignSegmentComboBox.SelectedIndex = 0;
        }

        CampaignStartDatePicker.SelectedDate = DateTime.Today;
        CampaignEndDatePicker.SelectedDate = DateTime.Today.AddDays(30);
        SaveCampaignButton.Content = "Créer la campagne";
    }

    private void CampaignChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private async void SaveCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var label = ReadOptional(CampaignLabelTextBox);

            if (label is null)
            {
                moduleContext.SetStatus("Le libellé de la campagne est requis.", isError: true);
                return;
            }

            if (CampaignChannelComboBox.SelectedItem is not EnumOption<CampaignChannel> { Value: { } channel })
            {
                moduleContext.SetStatus("Sélectionnez un canal.", isError: true);
                return;
            }

            var start = ReadDate(CampaignStartDatePicker);
            var end = ReadDate(CampaignEndDatePicker);

            if (start is null || end is null)
            {
                moduleContext.SetStatus("Les dates de début et de fin sont requises.", isError: true);
                return;
            }

            if (end < start)
            {
                moduleContext.SetStatus("La date de fin ne peut pas être antérieure à la date de début.", isError: true);
                return;
            }

            var segment = SelectedCode(CampaignSegmentComboBox);
            var objective = ReadOptional(CampaignObjectiveTextBox);
            var message = ReadOptional(CampaignMessageTextBox);

            if (editingCampaignCode is null)
            {
                var code = ReadOptional(CampaignCodeTextBox);

                if (code is null)
                {
                    moduleContext.SetStatus("Le code de la campagne est requis.", isError: true);
                    return;
                }

                var created = await moduleContext.ApiClient.CreateCrmCampaignAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateCampaignRequest(code, label, channel, start.Value, end.Value, segment, objective, message));

                moduleContext.SetStatus($"Campagne {created.Code} créée en brouillon.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateCrmCampaignAsync(
                    moduleContext.ApiBaseUrl,
                    editingCampaignCode,
                    new UpdateCampaignRequest(label, channel, start.Value, end.Value, segment, objective, message));

                moduleContext.SetStatus($"Campagne {updated.Code} mise à jour.");
            }

            ResetCampaignForm();
            await ReloadCampaignsAsync();
        });
    }

    private async void ScheduleCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        await TransitionCampaignAsync("schedule", null, "planifiée");
    }

    private async void LaunchCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        await TransitionCampaignAsync("launch", null, "lancée");
    }

    private async void CompleteCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        await TransitionCampaignAsync("complete", null, "terminée");
    }

    private async void CancelCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var reason = ReadOptional(CampaignCancelReasonTextBox);

        if (reason is null)
        {
            moduleContext.SetStatus("Le motif de l'annulation est obligatoire.", isError: true);
            return;
        }

        if (CampaignsDataGrid.SelectedItem is not CampaignRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez une campagne.", isError: true);
            return;
        }

        if (!Confirm(
                $"Annuler la campagne {selected.Code} ?\nCe qui a déjà atteint les clients ne peut pas être repris ; la campagne restera dans l'historique avec son motif.",
                "Annuler la campagne"))
        {
            return;
        }

        await TransitionCampaignAsync("cancel", new CancelCampaignRequest(reason), "annulée");
    }

    private async Task TransitionCampaignAsync(string transition, object? body, string doneLabel)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (CampaignsDataGrid.SelectedItem is not CampaignRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez une campagne.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var changed = await moduleContext.ApiClient.TransitionCrmCampaignAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                transition,
                body);

            CampaignCancelReasonTextBox.Text = string.Empty;
            moduleContext.SetStatus($"Campagne {changed.Code} {doneLabel}.");

            await ReloadCampaignsAsync();
        });
    }

    private async void LoadAudienceButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (CampaignsDataGrid.SelectedItem is not CampaignRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez une campagne.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var audience = await moduleContext.ApiClient.GetCrmCampaignAudienceAsync(
                moduleContext.ApiBaseUrl,
                selected.Code);

            AudienceDataGrid.ItemsSource = audience.Members;

            // Les exclusions sont affichees a cote du nombre atteint : sans elles, une
            // audience courte ressemble a une erreur de ciblage.
            var consent = audience.RequiresMarketingConsent
                ? $"{audience.ExcludedForConsent} exclu(s) faute de consentement, "
                : string.Empty;

            AudienceSummaryTextBlock.Text =
                $"{audience.Reachable} client(s) atteint(s) — {consent}"
                + $"{audience.ExcludedForMissingContact} sans coordonnée pour ce canal.";
        });
    }

    // ================================= Satisfaction =================================

    private async void RefreshNpsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ReloadSatisfactionAsync();
            context?.SetStatus("Satisfaction actualisée.");
        });
    }

    private async void NpsFilters_Changed(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadSatisfactionAsync);
    }

    private async Task ReloadSatisfactionAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var from = ReadDate(NpsFromDatePicker);
        var to = ReadDate(NpsToDatePicker);

        if (from is null || to is null)
        {
            moduleContext.SetStatus("Les dates de début et de fin sont requises.", isError: true);
            return;
        }

        if (from > to)
        {
            moduleContext.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var unitCode = SelectedCode(NpsUnitComboBox);

        var summary = await moduleContext.ApiClient.GetCrmNpsSummaryAsync(
            moduleContext.ApiBaseUrl,
            from.Value,
            to.Value,
            unitCode);

        NpsScoreTextBlock.Text = FormatDecimal(summary.Nps);
        NpsAnswersTextBlock.Text = summary.AnswerCount.ToString(CultureInfo.CurrentCulture);
        NpsPromotersTextBlock.Text = summary.Promoters.ToString(CultureInfo.CurrentCulture);
        NpsPassivesTextBlock.Text = summary.Passives.ToString(CultureInfo.CurrentCulture);
        NpsDetractorsTextBlock.Text = summary.Detractors.ToString(CultureInfo.CurrentCulture);
        NpsAverageTextBlock.Text = FormatDecimal(summary.AverageScore);

        NpsUnitsDataGrid.ItemsSource = summary.Units
            .Select(unit => new NpsUnitRowView(
                unit.HotelUnitCode,
                unit.HotelUnitName,
                unit.AnswerCount,
                unit.Promoters,
                unit.Detractors,
                FormatDecimal(unit.Nps)))
            .ToArray();

        var entries = await moduleContext.ApiClient.GetCrmSatisfactionAsync(
            moduleContext.ApiBaseUrl,
            from,
            to,
            unitCode,
            null,
            null);

        SurveysDataGrid.ItemsSource = entries.Select(ToRowView).ToArray();
    }

    private async void RecordSurveyButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var customerCode = ReadOptional(SurveyCustomerCodeTextBox);

            if (customerCode is null)
            {
                moduleContext.SetStatus("Le code du client est requis.", isError: true);
                return;
            }

            var unitCode = SelectedCode(SurveyUnitComboBox);

            if (unitCode is null)
            {
                moduleContext.SetStatus("Sélectionnez l'unité concernée.", isError: true);
                return;
            }

            // Borne du domaine (SatisfactionEntry) : verifiee ici pour eviter un
            // aller-retour API previsible.
            if (!TryReadInteger(SurveyScoreTextBox, out var score) || score is < 0 or > 10)
            {
                moduleContext.SetStatus("La note doit être un nombre entier compris entre 0 et 10.", isError: true);
                return;
            }

            if (SurveySourceComboBox.SelectedItem is not EnumOption<SatisfactionSource> { Value: { } source })
            {
                moduleContext.SetStatus("Sélectionnez la source de la réponse.", isError: true);
                return;
            }

            var surveyDate = ReadDate(SurveyDatePicker) ?? DateOnly.FromDateTime(DateTime.Today);

            await moduleContext.ApiClient.RecordCrmSatisfactionAsync(
                moduleContext.ApiBaseUrl,
                new RecordSatisfactionRequest(
                    customerCode,
                    unitCode,
                    surveyDate,
                    score,
                    source,
                    null,
                    ReadOptional(SurveyCommentTextBox)));

            SurveyCustomerCodeTextBox.Text = string.Empty;
            SurveyScoreTextBox.Text = string.Empty;
            SurveyCommentTextBox.Text = string.Empty;

            moduleContext.SetStatus($"Réponse enregistrée pour {customerCode}.");

            await ReloadSatisfactionAsync();
        });
    }

    // ================================== Contacts ==================================

    private async void RefreshInteractionsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ReloadInteractionsAsync();
            context?.SetStatus("Journal des contacts actualisé.");
        });
    }

    private async void InteractionFilters_Changed(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadInteractionsAsync);
    }

    private async void InteractionFilterCustomerTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RunAsync(ReloadInteractionsAsync);
    }

    private async Task ReloadInteractionsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var from = ReadDate(InteractionFromDatePicker);
        var to = ReadDate(InteractionToDatePicker);

        if (from.HasValue && to.HasValue && from > to)
        {
            moduleContext.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var interactions = await moduleContext.ApiClient.GetCrmInteractionsAsync(
            moduleContext.ApiBaseUrl,
            from,
            to,
            ReadOptional(InteractionFilterCustomerTextBox),
            null);

        InteractionsDataGrid.ItemsSource = interactions.Select(ToRowView).ToArray();
    }

    private async void LogInteractionButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var customerCode = ReadOptional(InteractionCustomerCodeTextBox);

            if (customerCode is null)
            {
                moduleContext.SetStatus("Le code du client est requis.", isError: true);
                return;
            }

            var subject = ReadOptional(InteractionSubjectTextBox);

            if (subject is null)
            {
                moduleContext.SetStatus("L'objet du contact est requis.", isError: true);
                return;
            }

            var handledBy = ReadOptional(InteractionHandledByTextBox);

            if (handledBy is null)
            {
                moduleContext.SetStatus("Indiquez qui a traité le contact.", isError: true);
                return;
            }

            if (InteractionChannelComboBox.SelectedItem is not EnumOption<InteractionChannel> { Value: { } channel } ||
                InteractionDirectionComboBox.SelectedItem is not EnumOption<InteractionDirection> { Value: { } direction })
            {
                moduleContext.SetStatus("Sélectionnez le canal et le sens du contact.", isError: true);
                return;
            }

            // La date saisie est une date metier ; l'heure du contact est celle de sa
            // consignation quand elle n'est pas connue autrement.
            var occurredOn = ReadDate(InteractionDatePicker);

            var occurredAt = occurredOn.HasValue
                ? new DateTimeOffset(occurredOn.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : DateTimeOffset.UtcNow;

            await moduleContext.ApiClient.LogCrmInteractionAsync(
                moduleContext.ApiBaseUrl,
                new LogGuestInteractionRequest(
                    customerCode,
                    occurredAt,
                    channel,
                    direction,
                    subject,
                    handledBy,
                    SelectedCode(InteractionUnitComboBox),
                    ReadOptional(InteractionNotesTextBox)));

            InteractionSubjectTextBox.Text = string.Empty;
            InteractionNotesTextBox.Text = string.Empty;

            moduleContext.SetStatus($"Contact consigné pour {customerCode}.");

            await ReloadInteractionsAsync();
        });
    }

    // =================================== Internes ===================================

    // Les boutons d'ecriture sont grises selon le droit qui les concerne ET selon ce
    // que la selection courante autorise : une campagne terminee ne se relance pas,
    // un mouvement de points sans compte charge n'a pas d'objet.
    private void UpdateActionButtons()
    {
        var selectedSegment = SegmentsDataGrid.SelectedItem as CustomerSegmentResponse;
        var selectedTier = TiersDataGrid.SelectedItem as LoyaltyTierResponse;
        var selectedCampaign = (CampaignsDataGrid.SelectedItem as CampaignRowView)?.Source;

        SaveProfileButton.IsEnabled = canWriteCrm && openCustomerCode is not null;
        GrantConsentButton.IsEnabled = canWriteCrm && openCustomerCode is not null;
        WithdrawConsentButton.IsEnabled = canWriteCrm && openCustomerCode is not null;

        SaveSegmentButton.IsEnabled = canWriteCrm;
        ActivateSegmentButton.IsEnabled = canWriteCrm && selectedSegment is { IsActive: false };
        DeactivateSegmentButton.IsEnabled = canWriteCrm && selectedSegment is { IsActive: true };

        SaveTierButton.IsEnabled = canWriteCrm;
        ActivateTierButton.IsEnabled = canWriteCrm && selectedTier is { IsActive: false };
        DeactivateTierButton.IsEnabled = canWriteCrm && selectedTier is { IsActive: true };

        RecordMovementButton.IsEnabled = canMoveLoyaltyPoints && loyaltyAccountCode is not null;

        SaveCampaignButton.IsEnabled = canWriteCrm && (editingCampaignCode is null || selectedCampaign?.CanEdit == true);
        ScheduleCampaignButton.IsEnabled = canWriteCrm && selectedCampaign?.Status == CampaignStatus.Draft;
        LaunchCampaignButton.IsEnabled = canWriteCrm && selectedCampaign?.Status == CampaignStatus.Scheduled;
        CompleteCampaignButton.IsEnabled = canWriteCrm && selectedCampaign?.Status == CampaignStatus.Running;
        CancelCampaignButton.IsEnabled = canWriteCrm
            && selectedCampaign is not null
            && selectedCampaign.Status is not (CampaignStatus.Completed or CampaignStatus.Cancelled);
        LoadAudienceButton.IsEnabled = selectedCampaign is not null;

        RecordSurveyButton.IsEnabled = canWriteCrm;
        LogInteractionButton.IsEnabled = canWriteCrm;

        ApplyPermissionHint(SaveProfileButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(GrantConsentButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(WithdrawConsentButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(SaveSegmentButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(ActivateSegmentButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(DeactivateSegmentButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(SaveTierButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(ActivateTierButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(DeactivateTierButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(SaveCampaignButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(ScheduleCampaignButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(LaunchCampaignButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(CompleteCampaignButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(CancelCampaignButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(RecordSurveyButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(LogInteractionButton, canWriteCrm, WritePermissionHint);
        ApplyPermissionHint(RecordMovementButton, canMoveLoyaltyPoints, LoyaltyPermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    // sinon un message pose pour un profil restreint survit a la reconnexion d'un
    // profil qui, lui, a le droit (les vues survivent a la deconnexion).
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // Periode par defaut des ecrans de periode : le mois en cours, comme les autres
    // modules de pilotage.
    private void ApplyDefaultDates()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        NpsFromDatePicker.SelectedDate = firstOfMonth;
        NpsToDatePicker.SelectedDate = today;
        InteractionFromDatePicker.SelectedDate = firstOfMonth;
        InteractionToDatePicker.SelectedDate = today;
        SurveyDatePicker.SelectedDate = today;
        InteractionDatePicker.SelectedDate = today;
        MovementDatePicker.SelectedDate = today;
    }

    private async Task RunAsync(Func<Task> action)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(action);
    }

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non - la touche Entree ne suffit jamais a engager
    // l'action.
    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static string? SelectedCode(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as CodeOption)?.Code;
    }

    private static void SelectCode(ComboBox comboBox, string? code)
    {
        if (comboBox.ItemsSource is not IEnumerable<CodeOption> options)
        {
            return;
        }

        var match = options.FirstOrDefault(option =>
            string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));

        comboBox.SelectedItem = match ?? options.FirstOrDefault();
    }

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly? ReadDate(DatePicker picker)
    {
        return picker.SelectedDate.HasValue ? DateOnly.FromDateTime(picker.SelectedDate.Value) : null;
    }

    private static bool TryReadInteger(TextBox textBox, out int value)
    {
        return int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    // Une valeur absente s'affiche en tiret, jamais en zero : « personne n'a repondu »
    // et « le score est nul » sont deux situations tres differentes.
    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.#", CultureInfo.CurrentCulture) : "—";
    }

    private static string FormatLocal(DateTimeOffset? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "—";
    }

    private static string DescribeMovementKind(LoyaltyTransactionKind kind)
    {
        return MovementKinds.FirstOrDefault(option => option.Value == kind)?.Label ?? kind.ToString();
    }

    private static string DescribeChannel(CampaignChannel channel)
    {
        return CampaignChannels.FirstOrDefault(option => option.Value == channel)?.Label ?? channel.ToString();
    }

    private static string DescribeStatus(CampaignStatus status)
    {
        return CampaignStatuses.FirstOrDefault(option => option.Value == status)?.Label ?? status.ToString();
    }

    private static string DescribeCategory(NpsCategory category)
    {
        return NpsCategories.FirstOrDefault(option => option.Value == category)?.Label ?? category.ToString();
    }

    private static string DescribeSource(SatisfactionSource source)
    {
        return SatisfactionSources.FirstOrDefault(option => option.Value == source)?.Label ?? source.ToString();
    }

    private static CampaignRowView ToRowView(CampaignResponse campaign)
    {
        return new CampaignRowView(
            campaign,
            campaign.Code,
            campaign.Label,
            DescribeChannel(campaign.Channel),
            campaign.TargetSegmentLabel ?? "Tout le fichier",
            $"{campaign.StartDate.ToString("d", CultureInfo.CurrentCulture)} → {campaign.EndDate.ToString("d", CultureInfo.CurrentCulture)}",
            DescribeStatus(campaign.Status));
    }

    private static SurveyRowView ToRowView(SatisfactionEntryResponse entry)
    {
        return new SurveyRowView(
            entry.SurveyDate,
            entry.CustomerName,
            entry.HotelUnitCode,
            entry.Score,
            DescribeCategory(entry.Category),
            DescribeSource(entry.Source),
            entry.Comment);
    }

    private static InteractionRowView ToRowView(GuestInteractionResponse interaction)
    {
        return new InteractionRowView(
            interaction.OccurredAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            interaction.CustomerName,
            InteractionChannels.FirstOrDefault(option => option.Value == interaction.Channel)?.Label
                ?? interaction.Channel.ToString(),
            InteractionDirections.FirstOrDefault(option => option.Value == interaction.Direction)?.Label
                ?? interaction.Direction.ToString(),
            interaction.Subject,
            interaction.HandledBy);
    }

    /// <summary>
    /// Choix d'une liste deroulante adossee a un enum du domaine. <c>Value</c> est
    /// nullable pour porter l'entree « tous / aucun » en tete des filtres.
    /// </summary>
    private sealed record EnumOption<T>(T? Value, string Label)
        where T : struct, Enum;

    /// <summary>Choix d'une liste deroulante adossee a un code metier (unite, segment).</summary>
    private sealed record CodeOption(string? Code, string Label);

    private sealed record GuestRowView(
        string CustomerCode,
        string CustomerName,
        string? SegmentLabel,
        int LoyaltyPoints,
        string? TierLabel);

    private sealed record MovementRowView(
        DateOnly OccurredOn,
        string KindLabel,
        int Points,
        string Reason,
        string? Reference,
        string CreatedBy);

    // La reponse d'origine reste portee par la ligne : le formulaire et les boutons de
    // cycle de vie la relisent sans refaire un appel.
    private sealed record CampaignRowView(
        CampaignResponse Source,
        string Code,
        string Label,
        string ChannelLabel,
        string SegmentLabel,
        string PeriodLabel,
        string StatusLabel);

    private sealed record SurveyRowView(
        DateOnly SurveyDate,
        string CustomerName,
        string HotelUnitCode,
        int Score,
        string CategoryLabel,
        string SourceLabel,
        string? Comment);

    private sealed record InteractionRowView(
        string OccurredAtLabel,
        string CustomerName,
        string ChannelLabel,
        string DirectionLabel,
        string Subject,
        string HandledBy);

    private sealed record NpsUnitRowView(
        string HotelUnitCode,
        string HotelUnitName,
        int AnswerCount,
        int Promoters,
        int Detractors,
        string NpsLabel);
}
