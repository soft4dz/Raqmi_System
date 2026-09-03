using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Settings;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Parametrage global, en trois sections de PORTEES differentes :
///   A. Etablissement    - parametrage serveur, partage par tous les postes.
///   B. Poste de travail - reglages locaux (URL de l'API, identifiants memorises).
///   C. Sante du systeme - etat du serveur et de la base, identite de la session,
///                         et purge du journal d'audit.
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres vues, et passe par
/// <see cref="ModuleViewContext.RunAsync"/> pour tout appel API.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Taux de TVA admis par le domaine (exonere, reduit, normal). Reference
    /// directement la constante metier : le taux par defaut du parametrage doit
    /// etre l'un de ceux qu'une ligne de facture acceptera.
    /// </summary>
    public static IReadOnlyList<decimal> VatRateOptions { get; } = InvoiceLine.AllowedVatRates.ToArray();

    private const string WritePermissionHint =
        "Permission settings.write requise : votre profil peut consulter le paramétrage de l'établissement, pas le modifier. Les champs ci-dessous sont en lecture seule.";

    private const string PurgePermissionHint =
        "Permission security.seed requise : la purge du journal d'audit est réservée à l'administration du socle de sécurité.";

    private const string PurgeDescription =
        "Supprime définitivement les entrées du journal d'audit antérieures à la rétention configurée ci-dessus. Les traces supprimées ne sont récupérables que depuis une sauvegarde.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons soumis a permission, capturees avant toute
    // substitution : la vue est reutilisee d'une session a l'autre, un message pose
    // pour un profil restreint ne doit pas survivre au profil suivant.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Permissions du profil connecte, relevees a l'ouverture de session. Le serveur
    // reste la seule autorite : ceci evite seulement de laisser saisir un formulaire
    // voue a un 403, ou de proposer une purge qui sera refusee.
    private bool canWriteSettings;
    private bool canPurgeAudit;

    // La liste des unites n'est lisible qu'avec units.read, et une seule fois par session :
    // sans cette cle le code se saisit, et aucun appel n'est fait.
    private bool stationUnitOptionsLoaded;

    // Dernier parametrage recu du serveur : sert a signaler, avant une purge, que
    // la retention affichee a l'ecran n'est pas celle qui est enregistree.
    private ApplicationSettingsResponse? loadedSettings;

    public SettingsView()
    {
        InitializeComponent();

        DefaultVatRateComboBox.ItemsSource = VatRateOptions;

        AuditRetentionHintTextBlock.Text =
            $"Entre {ApplicationSettings.MinimumAuditRetentionDays} et {ApplicationSettings.MaximumAuditRetentionDays} jours.";

        ClearForm();
        ClearDiagnostics();
        ApplyPermissions();
    }

    // ============================== Apparence ==============================

    // Coche la puce du mode enregistre. Le drapeau evite que le Checked declenche par ce
    // marquage ne soit pris pour un choix de l'utilisateur - il reappliquerait le meme
    // theme et reecrirait le fichier de reglages pour rien, a chaque ouverture de l'ecran.
    private bool syncingApparence;

    private void RefreshApparenceSection()
    {
        syncingApparence = true;

        try
        {
            var mode = DesktopSettings.LoadApparence();

            ApparenceSystemeRadio.IsChecked = mode == ApparenceMode.Systeme;
            ApparenceClairRadio.IsChecked = mode == ApparenceMode.Clair;
            ApparenceSombreRadio.IsChecked = mode == ApparenceMode.Sombre;

            var densite = DesktopSettings.LoadDensite();
            DensiteConfortableRadio.IsChecked = densite == ApparenceDensite.Confortable;
            DensiteCompactRadio.IsChecked = densite == ApparenceDensite.Compact;
        }
        finally
        {
            syncingApparence = false;
        }
    }

    private void DensiteRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (syncingApparence || (sender as RadioButton)?.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<ApparenceDensite>(tag, out var densite))
        {
            return;
        }

        ThemeManager.AppliquerDensite(System.Windows.Application.Current.Resources, densite);
        DesktopSettings.SaveDensite(densite);

        context?.SetStatus(densite == ApparenceDensite.Compact
            ? "Tableaux en affichage compact sur ce poste."
            : "Tableaux en affichage confortable sur ce poste.");
    }

    private void ApparenceRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (syncingApparence || (sender as RadioButton)?.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<ApparenceMode>(tag, out var mode))
        {
            return;
        }

        ThemeManager.Appliquer(System.Windows.Application.Current.Resources, mode);
        DesktopSettings.SaveApparence(mode);

        // L'icone de bascule de l'en-tete appartient a la fenetre, pas a cet ecran :
        // elle doit suivre, sinon elle proposerait de passer a un theme deja affiche.
        (Window.GetWindow(this) as MainWindow)?.RefreshApparenceToggle();

        var applique = mode switch
        {
            ApparenceMode.Clair => "Apparence claire activée sur ce poste.",
            ApparenceMode.Sombre => "Apparence sombre activée sur ce poste.",
            _ => "L'apparence suit désormais le réglage de Windows.",
        };

        context?.SetStatus(ThemeManager.RedemarrageConseille
            ? applique + " Les écrans déjà ouverts la prendront au prochain démarrage."
            : applique);
    }

    /// <summary>
    /// L'unite de ce poste vient de changer : la fenetre previent « Mon Espace », dont les
    /// files unitaires dependent de ce reglage.
    /// </summary>
    public event Action? StationUnitChanged;

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWriteSettings = moduleViewContext.HasPermission(PermissionCatalog.SettingsWrite);
        canPurgeAudit = moduleViewContext.HasPermission(PermissionCatalog.SecuritySeed);

        ApplyPermissions();
        RefreshWorkstationSection();
    }

    /// <summary>
    /// (Re)charge le parametrage serveur, l'identite de la session et l'etat de sante.
    /// Sort silencieusement tant qu'aucun contexte n'a ete fourni ou que la session
    /// n'est pas ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        RefreshWorkstationSection();

        await active.RunAsync(async () =>
        {
            await ReloadSettingsAsync(active);
            await ReloadSessionAsync(active);
            await ReloadHealthAsync(active);
            await ReloadStationUnitOptionsAsync(active);

            active.SetStatus("Paramétrage global chargé.");
        });
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue (appelee a la deconnexion) : le
    /// parametrage, l'identite de session et l'etat de sante de l'utilisateur
    /// precedent ne doivent jamais rester affiches pour le suivant.
    /// </summary>
    public void ResetState()
    {
        loadedSettings = null;

        // La liste des unites est une donnee SERVEUR lue avec les cles du profil qui part :
        // elle ne doit pas rester proposee au suivant.
        stationUnitOptionsLoaded = false;
        StationUnitComboBox.ItemsSource = null;
        StationUnitComboBox.Visibility = Visibility.Collapsed;
        StationUnitTextBox.Visibility = Visibility.Visible;

        ClearForm();
        ClearDiagnostics();
        RefreshWorkstationSection();
        ApplyPermissions();
    }

    // ========================= A. Etablissement (serveur) =========================

    private async void ReloadSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await ReloadSettingsAsync(active);
            active.SetStatus("Paramétrage de l'établissement actualisé.");
        });
    }

    private async Task ReloadSettingsAsync(ModuleViewContext active)
    {
        var settings = await active.ApiClient.GetApplicationSettingsAsync(active.ApiBaseUrl);

        loadedSettings = settings;
        FillForm(settings);
    }

    private void FillForm(ApplicationSettingsResponse settings)
    {
        CompanyNameTextBox.Text = settings.CompanyName;
        CompanyNifTextBox.Text = settings.CompanyNif ?? string.Empty;
        CompanyRcTextBox.Text = settings.CompanyRc ?? string.Empty;
        CompanyAiTextBox.Text = settings.CompanyAi ?? string.Empty;
        CompanyNisTextBox.Text = settings.CompanyNis ?? string.Empty;
        CompanyAddressTextBox.Text = settings.CompanyAddress ?? string.Empty;
        CompanyCityTextBox.Text = settings.CompanyCity ?? string.Empty;
        CompanyPhoneTextBox.Text = settings.CompanyPhone ?? string.Empty;
        CompanyEmailTextBox.Text = settings.CompanyEmail ?? string.Empty;

        // Un taux hors liste ne peut pas venir du serveur (le domaine le refuse),
        // mais l'afficher comme selectionne serait mensonger s'il arrivait : la
        // liste reste alors vide et l'enregistrement redemande un choix.
        DefaultVatRateComboBox.SelectedItem = VatRateOptions.Contains(settings.DefaultVatRate)
            ? settings.DefaultVatRate
            : null;

        CurrencyLabelTextBox.Text = settings.CurrencyLabel;
        AuditRetentionDaysTextBox.Text = settings.AuditRetentionDays.ToString(CultureInfo.CurrentCulture);

        UnconfiguredNoticeTextBlock.Text =
            "Aucun paramétrage n'a encore été enregistré : les valeurs ci-dessous sont celles par défaut de l'installation. "
            + $"Tant qu'elles ne sont pas enregistrées, toute facture émise portera « {ApplicationSettings.UnconfiguredCompanyName} » comme émetteur.";
        UnconfiguredNoticeBorder.Visibility = settings.IsConfigured ? Visibility.Collapsed : Visibility.Visible;

        SettingsTraceTextBlock.Text = BuildTraceText(settings);

        UpdatePurgeHint();
    }

    private static string BuildTraceText(ApplicationSettingsResponse settings)
    {
        if (!settings.IsConfigured)
        {
            return string.Empty;
        }

        var text = $"Enregistré le {FormatMoment(settings.CreatedAt)} par {settings.CreatedBy}.";

        if (settings.UpdatedAt is DateTimeOffset updatedAt)
        {
            text += $" Dernière modification le {FormatMoment(updatedAt)} par {settings.UpdatedBy ?? "—"}.";
        }

        return text;
    }

    private void ClearForm()
    {
        CompanyNameTextBox.Text = string.Empty;
        CompanyNifTextBox.Text = string.Empty;
        CompanyRcTextBox.Text = string.Empty;
        CompanyAiTextBox.Text = string.Empty;
        CompanyNisTextBox.Text = string.Empty;
        CompanyAddressTextBox.Text = string.Empty;
        CompanyCityTextBox.Text = string.Empty;
        CompanyPhoneTextBox.Text = string.Empty;
        CompanyEmailTextBox.Text = string.Empty;
        CurrencyLabelTextBox.Text = string.Empty;
        AuditRetentionDaysTextBox.Text = string.Empty;
        DefaultVatRateComboBox.SelectedItem = null;

        UnconfiguredNoticeBorder.Visibility = Visibility.Collapsed;
        SettingsTraceTextBlock.Text = string.Empty;

        UpdatePurgeHint();
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            if (!TryBuildUpdateRequest(active, out var request))
            {
                return;
            }

            var saved = await active.ApiClient.UpdateApplicationSettingsAsync(active.ApiBaseUrl, request);

            loadedSettings = saved;
            FillForm(saved);

            active.SetStatus($"Paramétrage de l'établissement enregistré : émetteur « {saved.CompanyName} ».");
        });
    }

    /// <summary>
    /// Controles de saisie alignes sur les regles REELLES de l'entite serveur
    /// (ApplicationSettings) : raison sociale obligatoire, NIF a 15 chiffres s'il
    /// est renseigne, taux de TVA parmi ceux du domaine, retention dans ses bornes.
    /// Ils evitent un aller-retour previsible ; le serveur revalide de toute facon.
    /// </summary>
    private bool TryBuildUpdateRequest(ModuleViewContext active, out UpdateApplicationSettingsRequest request)
    {
        request = null!;

        var companyName = CompanyNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(companyName))
        {
            active.SetStatus("La raison sociale de l'établissement est obligatoire.", isError: true);
            CompanyNameTextBox.Focus();
            return false;
        }

        var nif = ReadOptional(CompanyNifTextBox);

        if (nif is not null && (nif.Length != 15 || !nif.All(char.IsAsciiDigit)))
        {
            active.SetStatus("Le NIF de l'établissement doit comporter exactement 15 chiffres.", isError: true);
            CompanyNifTextBox.Focus();
            return false;
        }

        var email = ReadOptional(CompanyEmailTextBox);

        if (email is not null && !IsPlausibleEmail(email))
        {
            active.SetStatus("L'adresse de courriel de l'établissement est invalide.", isError: true);
            CompanyEmailTextBox.Focus();
            return false;
        }

        if (DefaultVatRateComboBox.SelectedItem is not decimal vatRate || !VatRateOptions.Contains(vatRate))
        {
            active.SetStatus("Sélectionnez un taux de TVA par défaut (0, 9 ou 19 %).", isError: true);
            return false;
        }

        if (!TryReadRetentionDays(out var retentionDays))
        {
            active.SetStatus(
                $"La rétention du journal d'audit doit être un nombre entier de jours, entre {ApplicationSettings.MinimumAuditRetentionDays} et {ApplicationSettings.MaximumAuditRetentionDays}.",
                isError: true);
            AuditRetentionDaysTextBox.Focus();
            return false;
        }

        request = new UpdateApplicationSettingsRequest(
            companyName,
            vatRate,
            retentionDays,
            nif,
            ReadOptional(CompanyRcTextBox),
            ReadOptional(CompanyAiTextBox),
            ReadOptional(CompanyNisTextBox),
            ReadOptional(CompanyAddressTextBox),
            ReadOptional(CompanyCityTextBox),
            ReadOptional(CompanyPhoneTextBox),
            email,
            ReadOptional(CurrencyLabelTextBox));

        return true;
    }

    private bool TryReadRetentionDays(out int retentionDays)
    {
        retentionDays = 0;

        var text = AuditRetentionDaysTextBox.Text.Trim();

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed) &&
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return false;
        }

        if (parsed < ApplicationSettings.MinimumAuditRetentionDays ||
            parsed > ApplicationSettings.MaximumAuditRetentionDays)
        {
            return false;
        }

        retentionDays = parsed;
        return true;
    }

    // ============================ B. Poste de travail ============================

    // Lit l'etat local du poste : URL enregistree (ou imposee par l'environnement)
    // et presence d'identifiants memorises. Aucun appel reseau.
    private void RefreshWorkstationSection()
    {
        ApiBaseUrlTextBox.Text = DesktopSettings.Load();
        RefreshApparenceSection();
        RefreshStationUnitSection();

        var hasCredentials = DesktopSettings.TryLoadCredentials(out var rememberedUser, out _);

        RememberedCredentialsTextBlock.Text = hasCredentials
            ? $"Identifiants mémorisés sur ce poste pour « {rememberedUser} »."
            : "Aucun identifiant mémorisé sur ce poste.";

        ForgetCredentialsButton.IsEnabled = hasCredentials;
        ForgetCredentialsButton.ToolTip = hasCredentials
            ? "Effacer de ce poste le nom d'utilisateur et le mot de passe mémorisés"
            : "Aucun identifiant n'est mémorisé sur ce poste.";

        if (DesktopSettings.ApiBaseUrlEnvironmentOverride is { } forcedUrl)
        {
            ApiUrlOverrideNoticeTextBlock.Text =
                $"La variable d'environnement {DesktopSettings.ApiBaseUrlEnvironmentVariable} impose « {forcedUrl} » : "
                + "elle est prioritaire sur la valeur enregistrée ici, qui restera donc sans effet tant qu'elle est définie.";
            ApiUrlOverrideNoticeBorder.Visibility = Visibility.Visible;
        }
        else
        {
            ApiUrlOverrideNoticeBorder.Visibility = Visibility.Collapsed;
        }
    }

    // ------------------------------ Unite de ce poste ------------------------------

    // Le SEUL endroit qui ecrit DesktopSettings.StationUnitCode. « Mon Espace » l'affiche et
    // renvoie ici : deux surfaces qui ecrivent le meme reglage finiraient par se contredire.
    private void RefreshStationUnitSection()
    {
        var code = DesktopSettings.LoadStationUnitCode();

        StationUnitCurrentTextBlock.Text = code is null
            ? "Ce poste n'est rattaché à aucune unité : Mon Espace ne compose aucune file d'unité (arrivées, départs, chambres, date métier, événements)."
            : $"Ce poste est rattaché à l'unité {code}.";

        StationUnitTextBox.Text = code ?? string.Empty;

        if (StationUnitComboBox.Visibility == Visibility.Visible)
        {
            StationUnitComboBox.SelectedValue = code;
        }

        ClearStationUnitButton.IsEnabled = code is not null;
        ClearStationUnitButton.ToolTip = code is not null
            ? "Ce poste ne sera plus rattaché à aucune unité"
            : "Ce poste n'est rattaché à aucune unité.";
    }

    // Liste des unites : lisible avec units.read seulement. Sans cette cle, aucun appel n'est
    // fait et le code se saisit - c'est le cas d'un poste de caisse.
    private async Task ReloadStationUnitOptionsAsync(ModuleViewContext active)
    {
        if (stationUnitOptionsLoaded || !active.HasPermission(PermissionCatalog.UnitsRead))
        {
            return;
        }

        var units = await active.ApiClient.GetHotelUnitsAsync(active.ApiBaseUrl, includeInactive: false);

        StationUnitComboBox.ItemsSource = units
            .Select(unit => new StationUnitOption(unit.Code, $"{unit.Code} · {unit.Name}"))
            .ToList();

        StationUnitComboBox.SelectedValue = DesktopSettings.LoadStationUnitCode();
        StationUnitComboBox.Visibility = Visibility.Visible;
        StationUnitTextBox.Visibility = Visibility.Collapsed;
        stationUnitOptionsLoaded = true;
    }

    private void SaveStationUnitButton_Click(object sender, RoutedEventArgs e)
    {
        var chosen = StationUnitComboBox.Visibility == Visibility.Visible
            ? StationUnitComboBox.SelectedValue as string
            : StationUnitTextBox.Text;

        if (string.IsNullOrWhiteSpace(chosen))
        {
            context?.SetStatus("Choisissez une unité, ou retirez l'unité de ce poste.", isError: true);
            return;
        }

        DesktopSettings.SaveStationUnitCode(chosen);
        RefreshStationUnitSection();
        StationUnitChanged?.Invoke();

        context?.SetStatus(
            $"Ce poste est rattaché à l'unité {chosen.Trim().ToUpperInvariant()}. Mon Espace en tiendra compte à son prochain chargement.");
    }

    private void ClearStationUnitButton_Click(object sender, RoutedEventArgs e)
    {
        DesktopSettings.SaveStationUnitCode(null);
        RefreshStationUnitSection();
        StationUnitChanged?.Invoke();

        context?.SetStatus("Ce poste n'est plus rattaché à une unité. Mon Espace en tiendra compte à son prochain chargement.");
    }

    // Reglage local : rien ne part vers le serveur, donc pas de RunAsync ici.
    private void SaveApiUrlButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ApiBaseUrlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            context?.SetStatus("L'URL de l'API est requise.", isError: true);
            return;
        }

        // Meme exigence que RaqmiApiClient.BuildUri : une adresse absolue, http ou
        // https. Refuser ici evite d'enregistrer une URL qui ne fonctionnera qu'au
        // prochain demarrage, quand plus personne ne fera le lien.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            context?.SetStatus("L'URL de l'API doit être une adresse absolue en http ou https.", isError: true);
            return;
        }

        DesktopSettings.Save(url);
        RefreshWorkstationSection();

        context?.SetStatus("URL de l'API enregistrée sur ce poste : elle sera utilisée au prochain démarrage.");
    }

    private void ForgetCredentialsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = Confirm(
            "Effacer de ce poste les identifiants mémorisés ?"
            + Environment.NewLine + Environment.NewLine
            + "L'écran de connexion ne sera plus pré-rempli : le nom d'utilisateur et le mot de passe devront être saisis à nouveau."
            + Environment.NewLine + Environment.NewLine
            + "Le compte lui-même n'est pas touché : seule la copie chiffrée conservée sur cet ordinateur est supprimée.",
            "Oublier les identifiants enregistrés");

        if (!confirmed)
        {
            return;
        }

        DesktopSettings.ClearCredentials();
        RefreshWorkstationSection();

        context?.SetStatus("Identifiants mémorisés effacés de ce poste.");
    }

    // =========================== C. Sante du systeme ===========================

    private async void CheckHealthButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await ReloadHealthAsync(active);

            if (active.ApiClient.IsAuthenticated)
            {
                await ReloadSessionAsync(active);
            }

            active.SetStatus("Vérification de la santé du système terminée.");
        });
    }

    /// <summary>
    /// Interroge les deux sondes publiques. Les indicateurs sont mis au rouge AVANT
    /// l'appel et repassent au vert a mesure des reponses : si le serveur ne repond
    /// pas du tout, l'exception remonte a RunAsync et l'ecran reste sur l'etat
    /// effectivement constate, jamais sur un vert perime.
    /// </summary>
    private async Task ReloadHealthAsync(ModuleViewContext active)
    {
        HealthCheckedAtTextBlock.Text =
            $"Dernière vérification : {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture)} — serveur {active.ApiBaseUrl}";

        SetHealthIndicator(
            ServerHealthDot,
            ServerHealthStateTextBlock,
            ServerHealthDetailTextBlock,
            HealthIndicator.Unhealthy,
            "Injoignable",
            "Aucune réponse du serveur pour l'instant.");

        SetHealthIndicator(
            DatabaseHealthDot,
            DatabaseHealthStateTextBlock,
            DatabaseHealthDetailTextBlock,
            HealthIndicator.Unhealthy,
            "Injoignable",
            "Aucune réponse du serveur pour l'instant.");

        var server = await active.ApiClient.GetServerHealthAsync(active.ApiBaseUrl);

        SetHealthIndicator(
            ServerHealthDot,
            ServerHealthStateTextBlock,
            ServerHealthDetailTextBlock,
            server.IsReachable ? HealthIndicator.Healthy : HealthIndicator.Unhealthy,
            server.IsReachable ? "Opérationnel" : "En erreur",
            server.IsReachable
                ? $"{server.Application ?? "Raqmi System"} — version {server.Version ?? "inconnue"}."
                : $"Le serveur a répondu HTTP {server.StatusCode}.");

        var database = await active.ApiClient.GetDatabaseHealthAsync(active.ApiBaseUrl);

        SetHealthIndicator(
            DatabaseHealthDot,
            DatabaseHealthStateTextBlock,
            DatabaseHealthDetailTextBlock,
            database.IsReachable ? HealthIndicator.Healthy : HealthIndicator.Unhealthy,
            database.IsReachable ? "Accessible" : "Injoignable",
            database.IsReachable
                ? $"Connexion établie ({database.Database ?? "base de données"})."
                : $"Le serveur applicatif répond, mais la base ne répond pas (HTTP {database.StatusCode}).");
    }

    private async Task ReloadSessionAsync(ModuleViewContext active)
    {
        var session = await active.ApiClient.GetCurrentSessionAsync(active.ApiBaseUrl);

        SessionUserTextBlock.Text = Display(session.UserName);
        SessionEmailTextBlock.Text = Display(session.Email);
        SessionRolesTextBlock.Text = session.Roles is { Count: > 0 } roles
            ? string.Join("  ·  ", roles)
            : "Aucun rôle";

        var permissions = session.Permissions ?? Array.Empty<string>();

        SessionPermissionCountTextBlock.Text = permissions.Count switch
        {
            0 => "Aucune permission",
            1 => "1 permission",
            _ => $"{permissions.Count.ToString(CultureInfo.CurrentCulture)} permissions"
        };

        SessionPermissionsTextBlock.Text = string.Join("  ·  ", permissions);
    }

    private void ClearDiagnostics()
    {
        SetHealthIndicator(
            ServerHealthDot,
            ServerHealthStateTextBlock,
            ServerHealthDetailTextBlock,
            HealthIndicator.Unknown,
            "Non vérifié",
            string.Empty);

        SetHealthIndicator(
            DatabaseHealthDot,
            DatabaseHealthStateTextBlock,
            DatabaseHealthDetailTextBlock,
            HealthIndicator.Unknown,
            "Non vérifié",
            string.Empty);

        HealthCheckedAtTextBlock.Text = "Aucune vérification effectuée depuis l'ouverture de la session.";

        SessionUserTextBlock.Text = "—";
        SessionEmailTextBlock.Text = "—";
        SessionRolesTextBlock.Text = "—";
        SessionPermissionCountTextBlock.Text = "—";
        SessionPermissionsTextBlock.Text = string.Empty;
    }

    private void SetHealthIndicator(
        Border dot,
        TextBlock state,
        TextBlock detail,
        HealthIndicator indicator,
        string label,
        string detailText)
    {
        var brushKey = indicator switch
        {
            HealthIndicator.Healthy => "StatusValidatedForegroundBrush",
            HealthIndicator.Unhealthy => "DangerBrush",
            _ => "TextPlaceholderBrush"
        };

        if (TryFindResource(brushKey) is Brush brush)
        {
            dot.Background = brush;
        }

        state.Text = label;
        detail.Text = detailText;
        detail.Visibility = string.IsNullOrEmpty(detailText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void PurgeAuditButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        if (!TryReadRetentionDays(out var retentionDays))
        {
            active.SetStatus(
                $"La rétention doit être un nombre entier de jours, entre {ApplicationSettings.MinimumAuditRetentionDays} et {ApplicationSettings.MaximumAuditRetentionDays}, avant de pouvoir purger.",
                isError: true);
            AuditRetentionDaysTextBox.Focus();
            return;
        }

        var threshold = DateTime.Today.AddDays(-retentionDays);

        var message =
            "Purger le journal d'audit ?"
            + Environment.NewLine + Environment.NewLine
            + $"Toutes les entrées d'audit de plus de {retentionDays.ToString(CultureInfo.CurrentCulture)} jours — c'est-à-dire antérieures au {threshold.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} — seront supprimées de la base."
            + Environment.NewLine + Environment.NewLine
            + "Cette action est IRRÉVERSIBLE : les traces supprimées ne peuvent être retrouvées que dans une sauvegarde de la base.";

        // La retention affichee peut avoir ete modifiee sans etre enregistree : purger
        // sur une valeur que personne n'a validee serait le pire moment pour decouvrir
        // l'ecart.
        if (loadedSettings is { } settings && settings.AuditRetentionDays != retentionDays)
        {
            message += Environment.NewLine + Environment.NewLine
                + $"Attention : la rétention affichée ({retentionDays.ToString(CultureInfo.CurrentCulture)} j) n'est pas celle enregistrée sur le serveur ({settings.AuditRetentionDays.ToString(CultureInfo.CurrentCulture)} j). C'est la valeur affichée qui sera appliquée.";
        }

        if (!Confirm(message, "Purger le journal d'audit"))
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var result = await active.ApiClient.PurgeAuditLogAsync(active.ApiBaseUrl, retentionDays);

            active.SetStatus(result.DeletedCount == 0
                ? "Journal d'audit : aucune entrée antérieure au seuil, rien n'a été supprimé."
                : $"Journal d'audit purgé : {result.DeletedCount.ToString(CultureInfo.CurrentCulture)} entrée(s) supprimée(s), antérieures au {FormatMoment(result.Threshold)}.");
        });
    }

    // ================================= Outils =================================

    // Un profil sans settings.write voit les champs en LECTURE SEULE (et non
    // desactives : la valeur reste lisible, selectionnable et copiable), avec la
    // mention qui explique pourquoi. Meme principe pour la purge, conditionnee a
    // security.seed - et non a settings.write.
    private void ApplyPermissions()
    {
        foreach (var textBox in new[]
                 {
                     CompanyNameTextBox, CompanyNifTextBox, CompanyRcTextBox, CompanyAiTextBox,
                     CompanyNisTextBox, CompanyAddressTextBox, CompanyCityTextBox, CompanyPhoneTextBox,
                     CompanyEmailTextBox, CurrencyLabelTextBox, AuditRetentionDaysTextBox
                 })
        {
            textBox.IsReadOnly = !canWriteSettings;
        }

        // Une liste deroulante n'a pas d'etat "lecture seule" : la desactiver est le
        // seul moyen d'empecher un changement de taux.
        DefaultVatRateComboBox.IsEnabled = canWriteSettings;

        SaveSettingsButton.IsEnabled = canWriteSettings;

        // Affectation SYMETRIQUE : cette vue survit a la deconnexion et est
        // reinitialisee sur la meme instance a chaque connexion. Poser le message
        // sans jamais le retirer ferait lire "permission requise" a l'utilisateur
        // suivant, qui a pourtant le droit - et dont le bouton est actif.
        ApplyPermissionHint(SaveSettingsButton, canWriteSettings, WritePermissionHint);

        ReadOnlyNoticeTextBlock.Text = WritePermissionHint;
        ReadOnlyNoticeBorder.Visibility = canWriteSettings ? Visibility.Collapsed : Visibility.Visible;

        PurgeAuditButton.IsEnabled = canPurgeAudit;
        ApplyPermissionHint(PurgeAuditButton, canPurgeAudit, PurgePermissionHint);

        UpdatePurgeHint();
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present.
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private void UpdatePurgeHint()
    {
        if (!canPurgeAudit)
        {
            PurgeHintTextBlock.Text = PurgePermissionHint;
            return;
        }

        PurgeHintTextBlock.Text = TryReadRetentionDays(out var retentionDays)
            ? PurgeDescription + $" Rétention actuellement affichée : {retentionDays.ToString(CultureInfo.CurrentCulture)} jours."
            : PurgeDescription;
    }

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static bool IsPlausibleEmail(string email)
    {
        var atIndex = email.IndexOf('@');

        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    private enum HealthIndicator
    {
        Unknown,
        Healthy,
        Unhealthy
    }
}

/// <summary>Une unite proposee dans le champ « Unité de ce poste ».</summary>
public sealed record StationUnitOption(string Code, string Label);
