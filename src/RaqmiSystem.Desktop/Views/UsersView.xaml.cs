using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Administration et utilisateurs : liste des comptes, creation et
/// modification d'une fiche, affectation des roles, et les actions de securite
/// (activation, desactivation, deverrouillage, reinitialisation du mot de passe).
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres vues, et passe par
/// <see cref="ModuleViewContext.RunAsync"/> pour tout appel API.
///
/// Les garde-fous anti-verrouillage (on ne desactive pas son propre compte, on ne
/// se retire pas la permission users.write, le dernier administrateur actif est
/// intouchable) sont ceux du SERVEUR, ou aucun appelant ne peut les contourner.
/// Ce que fait cette vue n'en est que le MIROIR : elle grise ce qui sera refuse
/// pour eviter de le decouvrir apres coup. Si un cas lui echappe, le refus du
/// serveur remonte de toute facon en message d'etat via RunAsync.
/// </summary>
public partial class UsersView : UserControl
{
    private const string WritePermissionHint =
        "Permission users.write requise : votre profil peut consulter les comptes, pas les administrer.";

    private const string SelectionHint =
        "Sélectionnez un compte dans la liste ci-dessous.";

    private const string SelfDeactivationHint =
        "Vous ne pouvez pas désactiver votre propre compte : demandez-le à un autre administrateur. Cette règle est appliquée par le serveur.";

    private const string CreationRolesHint =
        "Les rôles cochés seront accordés au compte au moment de sa création.";

    private const string EditionRolesHint =
        "Cochez les rôles à accorder, puis enregistrez. L'enregistrement REMPLACE l'ensemble des rôles du compte : ce qui n'est pas coché est retiré.";

    private const string NoSelectionPermissionsHint =
        "Sélectionnez un compte pour voir les permissions que ses rôles lui accordent.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons conditionnes, capturees avant toute
    // substitution : la vue survit a la deconnexion et sert la session suivante.
    // Poser un message d'explication sans jamais le retirer ferait lire
    // "permission requise" a un profil qui detient pourtant le droit.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Null en mode creation, identifiant du compte edite en mode modification.
    private Guid? editingUserId;

    // Le profil connecte peut-il administrer les comptes ? Releve a l'ouverture
    // de session : les actions d'ecriture sont grisees sinon, plutot que de
    // laisser decouvrir un 403 apres avoir rempli la fiche.
    private bool canWriteUsers;

    // Catalogue des roles proposes et libelles francais des permissions, charges
    // une fois par session (voir EnsureReferenceDataAsync).
    private IReadOnlyList<RoleSummary> roleCatalog = [];
    private IReadOnlyDictionary<string, string> permissionLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Vrai pendant le remplacement de la grille : la selection saute alors a null
    // puis revient, et ces deux evenements ne doivent pas relancer, depuis le
    // gestionnaire de selection, un appel API a l'interieur de celui en cours.
    private bool isRefreshingUsers;

    public UsersView()
    {
        InitializeComponent();

        ApplyCreationMode();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWriteUsers = moduleViewContext.HasPermission(PermissionCatalog.UsersWrite);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge le catalogue des roles puis la liste des comptes. Appelee a la
    /// premiere ouverture de l'onglet et par le bouton Actualiser. Sort
    /// silencieusement tant qu'aucun contexte n'est disponible ou qu'aucune
    /// session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await EnsureReferenceDataAsync(active);
            await ReloadUsersAsync(active);
        });
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue (appelee a la deconnexion) : ni la
    /// liste des comptes, ni les roles, ni surtout un mot de passe temporaire
    /// encore affiche ne doivent survivre a la session qui se ferme.
    /// </summary>
    public void ResetState()
    {
        roleCatalog = [];
        permissionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        UsersDataGrid.ItemsSource = null;
        SearchTextBox.Text = string.Empty;
        IncludeInactiveUsersCheckBox.IsChecked = false;
        UserCountTextBlock.Text = string.Empty;
        RolesEmptyTextBlock.Visibility = Visibility.Collapsed;

        ClearTemporaryPassword();
        ApplyCreationMode();
        UpdateActionButtons();
    }

    // Quitter l'onglet efface le mot de passe temporaire : un secret affiche une
    // seule fois n'a pas a attendre sur un ecran que plus personne ne regarde.
    private void UsersView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            ClearTemporaryPassword();
        }
    }

    // ============================== Chargements ==============================

    private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadWithStatusAsync();
    }

    private async void IncludeInactiveUsersCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadWithStatusAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ReloadWithStatusAsync();
    }

    private async Task ReloadWithStatusAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await EnsureReferenceDataAsync(active);
            await ReloadUsersAsync(active);
            active.SetStatus("Liste des comptes actualisée.");
        });
    }

    /// <summary>
    /// Charge le catalogue des roles et les libelles des permissions. Le
    /// catalogue des permissions est fige a la compilation du serveur et aucune
    /// route ne cree de role : les relire a chaque actualisation ne changerait
    /// rien. Ils sont donc lus une fois par session, et oublies a la deconnexion.
    /// </summary>
    private async Task EnsureReferenceDataAsync(ModuleViewContext active)
    {
        if (roleCatalog.Count > 0)
        {
            return;
        }

        var roles = await active.ApiClient.GetRolesAsync(active.ApiBaseUrl);
        var permissions = await active.ApiClient.GetPermissionCatalogAsync(active.ApiBaseUrl);

        roleCatalog = roles.ToArray();
        permissionLabels = permissions.ToDictionary(
            permission => permission.Key,
            permission => permission.Name,
            StringComparer.OrdinalIgnoreCase);

        RolesEmptyTextBlock.Visibility = roleCatalog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Les cases a cocher n'existaient pas encore : elles sont construites ici,
        // en conservant ce qui etait deja coche (rien, a la premiere ouverture).
        RefreshRoleOptions(ReadCheckedRoleNames());
    }

    private async Task ReloadUsersAsync(ModuleViewContext active, Guid? selectUserId = null)
    {
        var search = string.IsNullOrWhiteSpace(SearchTextBox.Text) ? null : SearchTextBox.Text.Trim();

        // La ligne selectionnee est identifiee par son id pour etre restauree
        // apres le rechargement : sans cela, activer ou desactiver un compte fait
        // perdre la selection et l'administrateur doit retrouver sa ligne.
        var targetId = selectUserId ?? (UsersDataGrid.SelectedItem as UserRowView)?.Id;

        var users = await active.ApiClient.GetUsersAsync(
            active.ApiBaseUrl,
            search,
            IncludeInactiveUsersCheckBox.IsChecked == true);

        var rows = users.Select(ToRowView).ToArray();

        isRefreshingUsers = true;

        try
        {
            UsersDataGrid.ItemsSource = rows;

            // Le compte edite peut etre sorti du filtre (recherche restreinte,
            // case "desactives" decochee) : le formulaire repart alors en
            // creation, plutot que de rester en modification sur une ligne qui
            // n'est plus a l'ecran.
            if (targetId is { } userId && !RestoreSelection(rows, userId))
            {
                ApplyCreationMode();
            }
        }
        finally
        {
            isRefreshingUsers = false;
        }

        UserCountTextBlock.Text = users.Count == 1
            ? "1 compte"
            : $"{users.Count.ToString(CultureInfo.CurrentCulture)} comptes";

        // Le detail (permissions effectives, trace) n'a pas ete demande par le
        // gestionnaire de selection, neutralise ci-dessus : il l'est ici, dans le
        // meme appel deja encadre par RunAsync.
        await RefreshDetailForSelectionAsync(active);

        UpdateActionButtons();
    }

    // L'id du compte est la cle stable d'une ligne a l'autre : la selection est
    // rendue sur cet id. Renvoie false quand la ligne n'est plus dans la liste,
    // pour que l'appelant sache que le compte edite a disparu de l'ecran.
    private bool RestoreSelection(IReadOnlyList<UserRowView> rows, Guid id)
    {
        var restored = rows.FirstOrDefault(row => row.Id == id);

        if (restored is null)
        {
            return false;
        }

        UsersDataGrid.SelectedItem = restored;
        UsersDataGrid.ScrollIntoView(restored);

        return true;
    }

    private async Task RefreshDetailForSelectionAsync(ModuleViewContext active)
    {
        if (UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            ClearDetail();
            return;
        }

        ApplyDetail(await active.ApiClient.GetUserAsync(active.ApiBaseUrl, selected.Id));
    }

    // ============================== Selection ==============================

    private async void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Changer de compte efface le mot de passe temporaire encore affiche :
        // il appartient a la ligne pour laquelle il a ete genere, et nulle part
        // ailleurs. Les actions qui en produisent un le reaffichent APRES avoir
        // recharge la liste, donc apres ce nettoyage.
        ClearTemporaryPassword();

        if (UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            // Pendant un rechargement, la selection tombe a null le temps de
            // remplacer la grille. Ce passage transitoire ne doit pas vider le
            // formulaire, qui peut porter une creation en cours de saisie :
            // ReloadUsersAsync decide lui-meme s'il faut y revenir.
            if (!isRefreshingUsers)
            {
                ApplyCreationMode();
            }

            UpdateActionButtons();
            return;
        }

        FillForm(selected);
        UpdateActionButtons();

        if (isRefreshingUsers)
        {
            return;
        }

        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(() => RefreshDetailForSelectionAsync(active));
    }

    private void NewUserButton_Click(object sender, RoutedEventArgs e)
    {
        ClearTemporaryPassword();
        ApplyCreationMode();

        // Deselectionner declenche le gestionnaire ci-dessus, qui repasse lui
        // aussi en mode creation : l'operation est idempotente.
        UsersDataGrid.SelectedItem = null;

        UpdateActionButtons();
        UserNameTextBox.Focus();
    }

    // Selectionner une ligne bascule le formulaire en mode modification.
    private void FillForm(UserRowView selected)
    {
        editingUserId = selected.Id;

        FormTitleTextBlock.Text = $"Modifier « {selected.UserName} »";
        UserNameTextBox.Text = selected.UserName;

        // Lecture seule plutot que desactive : l'identifiant de connexion reste
        // lisible et copiable. Il est fige cote serveur (il figure dans chaque
        // trace d'audit et dans les jetons deja emis).
        UserNameTextBox.IsReadOnly = true;
        UserNameHintTextBlock.Text = "Identifiant de connexion — figé : un compte qui doit changer de login est un nouveau compte.";

        DisplayNameTextBox.Text = selected.DisplayName;
        EmailTextBox.Text = selected.Email;
        SaveUserButton.Content = "Enregistrer les modifications";

        RolesTitleTextBlock.Text = $"Rôles de « {selected.UserName} »";
        RolesHintTextBlock.Text = EditionRolesHint;

        // Coche immediatement d'apres la ligne de liste ; le detail charge juste
        // apres confirme la meme information et y ajoute les permissions.
        RefreshRoleOptions(selected.Roles);
    }

    // Remet le formulaire, le selecteur de roles et le detail en mode creation.
    // Ne touche pas a la selection de la grille : c'est le gestionnaire de
    // selection qui l'appelle quand la selection tombe.
    private void ApplyCreationMode()
    {
        editingUserId = null;

        FormTitleTextBlock.Text = "Nouveau compte";
        UserNameTextBox.Text = string.Empty;
        UserNameTextBox.IsReadOnly = false;
        UserNameHintTextBlock.Text = "Choisissez-le avec soin : il ne pourra plus être modifié ensuite.";

        DisplayNameTextBox.Text = string.Empty;
        EmailTextBox.Text = string.Empty;
        SaveUserButton.Content = "Créer le compte";

        RolesTitleTextBlock.Text = "Rôles du nouveau compte";
        RolesHintTextBlock.Text = CreationRolesHint;

        RefreshRoleOptions([]);
        ClearDetail();
    }

    private void ApplyDetail(UserAccountDetailResponse detail)
    {
        // Reponse faisant autorite sur les roles reellement portes par le compte.
        RefreshRoleOptions(detail.Roles);

        EffectivePermissionsTextBlock.Text = detail.Permissions.Count == 0
            ? "Aucune : ce compte peut se connecter, mais aucun module ne lui est ouvert."
            : string.Join("  ·  ", detail.Permissions.Select(DescribePermission));

        AccountTraceTextBlock.Text = BuildTraceText(detail);
    }

    private void ClearDetail()
    {
        EffectivePermissionsTextBlock.Text = NoSelectionPermissionsHint;
        AccountTraceTextBlock.Text = string.Empty;
    }

    private static string BuildTraceText(UserAccountDetailResponse detail)
    {
        var text = $"Créé le {FormatMoment(detail.CreatedAt)} par {detail.CreatedBy}.";

        if (detail.UpdatedAt is DateTimeOffset updatedAt)
        {
            text += $" Dernière modification le {FormatMoment(updatedAt)} par {detail.UpdatedBy ?? "—"}.";
        }

        if (detail.IsLockedOut && detail.LockedOutUntil is DateTimeOffset lockedOutUntil)
        {
            text += $" Verrouillé jusqu'au {FormatMoment(lockedOutUntil)} après plusieurs échecs de connexion.";
        }

        return text;
    }

    // ============================== Fiche du compte ==============================

    private async void SaveUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var displayName = DisplayNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                active.SetStatus("Le nom affiché est obligatoire.", isError: true);
                DisplayNameTextBox.Focus();
                return;
            }

            if (!IsPlausibleEmail(email))
            {
                active.SetStatus("Une adresse de courriel valide est obligatoire.", isError: true);
                EmailTextBox.Focus();
                return;
            }

            if (editingUserId is { } userId)
            {
                var updated = await active.ApiClient.UpdateUserAsync(
                    active.ApiBaseUrl,
                    userId,
                    new UpdateUserRequest(email, displayName));

                await ReloadUsersAsync(active, updated.Id);
                active.SetStatus($"Compte « {updated.UserName} » mis à jour.");
                return;
            }

            var userName = UserNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(userName))
            {
                active.SetStatus("L'identifiant de connexion est obligatoire.", isError: true);
                UserNameTextBox.Focus();
                return;
            }

            // Le mot de passe n'est jamais choisi par l'administrateur : le
            // serveur en genere un temporaire et le renvoie une seule fois.
            var created = await active.ApiClient.CreateUserAsync(
                active.ApiBaseUrl,
                new CreateUserRequest(userName, email, displayName, ReadCheckedRoleNames()));

            // Recharger AVANT d'afficher le secret : le rechargement change la
            // selection, et le gestionnaire de selection efface justement tout
            // mot de passe temporaire encore a l'ecran.
            // Le finally est essentiel : ce mot de passe n'est renvoye QU'UNE FOIS
            // par le serveur. Si le rechargement echoue (reseau coupe entre les
            // deux appels), le compte existe deja mais le secret serait perdu
            // sans recours - l'affichage ne doit donc dependre d'aucun autre appel.
            try
            {
                await ReloadUsersAsync(active, created.User.Id);
            }
            finally
            {
                ShowTemporaryPassword(created.User.UserName, created.TemporaryPassword);
            }

            active.SetStatus($"Compte « {created.User.UserName} » créé. Remettez-lui le mot de passe temporaire affiché ci-dessus.");
        });
    }

    // ============================== Actions de securite ==============================

    private async void ActivateUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var changed = await active.ApiClient.SetUserActiveAsync(active.ApiBaseUrl, selected.Id, isActive: true);

            await ReloadUsersAsync(active, changed.Id);
            active.SetStatus($"Compte « {changed.UserName} » activé.");
        });
    }

    private async void DeactivateUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            return;
        }

        var confirmed = Confirm(
            $"Désactiver le compte « {selected.UserName} » ({selected.DisplayName}) ?"
            + Environment.NewLine + Environment.NewLine
            + "Il ne pourra plus se connecter. Ses traces d'audit sont conservées, et le compte pourra être réactivé plus tard.",
            "Désactiver le compte");

        if (!confirmed)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var changed = await active.ApiClient.SetUserActiveAsync(active.ApiBaseUrl, selected.Id, isActive: false);

            await ReloadUsersAsync(active, changed.Id);
            active.SetStatus($"Compte « {changed.UserName} » désactivé.");
        });
    }

    private async void UnlockUserButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var changed = await active.ApiClient.UnlockUserAsync(active.ApiBaseUrl, selected.Id);

            await ReloadUsersAsync(active, changed.Id);
            active.SetStatus($"Verrouillage levé pour le compte « {changed.UserName} » : il peut se reconnecter immédiatement.");
        });
    }

    private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            return;
        }

        var message =
            $"Réinitialiser le mot de passe du compte « {selected.UserName} » ({selected.DisplayName}) ?"
            + Environment.NewLine + Environment.NewLine
            + "Son mot de passe actuel cessera IMMÉDIATEMENT de fonctionner. Un mot de passe temporaire sera généré et affiché une seule fois : il faudra le lui remettre, et il devra le changer à sa première connexion.";

        if (active.CurrentUserId == selected.Id)
        {
            message += Environment.NewLine + Environment.NewLine
                + "Attention : il s'agit de VOTRE compte. Vous devrez vous reconnecter avec le mot de passe temporaire affiché.";
        }

        if (!Confirm(message, "Réinitialiser le mot de passe"))
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var reset = await active.ApiClient.ResetUserPasswordAsync(active.ApiBaseUrl, selected.Id);

            // Meme ordre - et meme garantie - qu'a la creation : le secret n'est
            // renvoye qu'une fois, son affichage ne doit dependre d'aucun appel
            // ulterieur susceptible d'echouer.
            try
            {
                await ReloadUsersAsync(active, selected.Id);
            }
            finally
            {
                ShowTemporaryPassword(selected.UserName, reset.TemporaryPassword);
            }

            active.SetStatus($"Mot de passe du compte « {selected.UserName} » réinitialisé. Remettez-lui le mot de passe temporaire affiché ci-dessus.");
        });
    }

    // ============================== Roles ==============================

    private async void SaveRolesButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || UsersDataGrid.SelectedItem is not UserRowView selected)
        {
            return;
        }

        var requested = ReadCheckedRoleNames();

        var message =
            $"Remplacer les rôles du compte « {selected.UserName} » ({selected.DisplayName}) ?"
            + Environment.NewLine + Environment.NewLine
            + $"Avant : {DescribeRoleSet(selected.Roles)}"
            + Environment.NewLine
            + $"Après : {DescribeRoleSet(requested)}"
            + Environment.NewLine + Environment.NewLine
            + "Ce remplacement retire tout rôle non coché, et donc les permissions qui en découlaient.";

        if (active.CurrentUserId == selected.Id)
        {
            message += Environment.NewLine + Environment.NewLine
                + "Attention : il s'agit de VOTRE compte. Le serveur refusera de vous retirer le rôle qui porte la permission users.write, pour ne pas vous fermer la porte de l'administration.";
        }

        if (!Confirm(message, "Modifier les rôles"))
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var changed = await active.ApiClient.SetUserRolesAsync(active.ApiBaseUrl, selected.Id, requested);

            await ReloadUsersAsync(active, changed.Id);
            active.SetStatus($"Rôles du compte « {changed.UserName} » enregistrés : {DescribeRoleSet(changed.Roles)}.");
        });
    }

    // Reconstruit la liste de cases a cocher a partir du catalogue des roles.
    // Reconstruire plutot que muter evite d'avoir a notifier l'interface : les
    // objets affiches sont neufs, et l'etat coche vient de l'appelant.
    private void RefreshRoleOptions(IReadOnlyCollection<string> selectedRoleNames)
    {
        RolesItemsControl.ItemsSource = roleCatalog
            .Select(role => new RoleOptionView(
                role.Name,
                role.DisplayName,
                DescribeRoleOption(role),
                selectedRoleNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private string[] ReadCheckedRoleNames()
    {
        return RolesItemsControl.ItemsSource is IEnumerable<RoleOptionView> options
            ? options.Where(option => option.IsSelected).Select(option => option.Name).ToArray()
            : [];
    }

    private static string DescribeRoleOption(RoleSummary role)
    {
        var description = string.IsNullOrWhiteSpace(role.Description)
            ? "Aucune description enregistrée pour ce rôle."
            : role.Description;

        // Un role systeme est fourni par l'installation : le signaler evite de le
        // confondre avec un role cree pour l'etablissement.
        return role.IsSystem ? "Rôle système — " + description : description;
    }

    private string DescribeRole(string roleName)
    {
        var role = roleCatalog.FirstOrDefault(current =>
            string.Equals(current.Name, roleName, StringComparison.OrdinalIgnoreCase));

        return role?.DisplayName ?? roleName;
    }

    private string DescribeRoleSet(IReadOnlyCollection<string> roleNames)
    {
        return roleNames.Count == 0
            ? "aucun rôle"
            : string.Join(", ", roleNames.Select(DescribeRole));
    }

    private string DescribePermission(string permissionKey)
    {
        return permissionLabels.TryGetValue(permissionKey, out var label) ? label : permissionKey;
    }

    // ============================== Mot de passe temporaire ==============================

    private void ShowTemporaryPassword(string userName, string temporaryPassword)
    {
        TemporaryPasswordCaptionTextBlock.Text =
            $"Compte « {userName} » — à remettre à son titulaire par un canal sûr (jamais par courriel non chiffré).";
        TemporaryPasswordTextBox.Text = temporaryPassword;
        TemporaryPasswordBorder.Visibility = Visibility.Visible;
    }

    private void ClearTemporaryPassword()
    {
        TemporaryPasswordTextBox.Text = string.Empty;
        TemporaryPasswordCaptionTextBlock.Text = string.Empty;
        TemporaryPasswordBorder.Visibility = Visibility.Collapsed;
    }

    private void CopyTemporaryPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var temporaryPassword = TemporaryPasswordTextBox.Text;

        if (string.IsNullOrEmpty(temporaryPassword))
        {
            return;
        }

        try
        {
            // Clipboard.SetText laisserait le mot de passe dans l'historique du
            // presse-papiers (Win+V) et dans le presse-papiers cloud synchronise
            // entre appareils : il y survivrait a tout l'effacement soigne fait
            // par ailleurs dans cette vue. Ces trois formats sont ceux que les
            // gestionnaires de mots de passe posent pour demander a Windows de
            // ne pas conserver ni synchroniser la valeur.
            var data = new DataObject(DataFormats.UnicodeText, temporaryPassword);
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
            data.SetData("CanIncludeInClipboardHistory", false);
            data.SetData("CanUploadToCloudClipboard", false);

            Clipboard.SetDataObject(data, copy: true);
            context?.SetStatus("Mot de passe temporaire copié dans le presse-papiers (exclu de l'historique Windows).");
        }
        catch (ExternalException ex)
        {
            // Le presse-papiers est une ressource partagee de Windows : une autre
            // application peut le tenir verrouille. Le dire, plutot que de laisser
            // croire a une copie qui n'a pas eu lieu - le mot de passe reste
            // affiche et selectionnable a la main.
            context?.SetStatus($"Copie impossible : le presse-papiers est indisponible ({ex.Message}). Sélectionnez le mot de passe à la souris.", isError: true);
        }
    }

    private void HideTemporaryPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        ClearTemporaryPassword();
    }

    // ============================== Etat des actions ==============================

    /// <summary>
    /// Conditionne chaque action a la permission users.write ET a l'etat du
    /// compte selectionne, en disant a chaque fois POURQUOI l'action est grisee.
    /// Le garde-fou "on ne desactive pas son propre compte" est ici un miroir de
    /// celui du serveur : il evite de tenter l'action, il ne la remplace pas.
    /// </summary>
    private void UpdateActionButtons()
    {
        var selected = UsersDataGrid.SelectedItem as UserRowView;
        var isSelf = selected is not null && context?.CurrentUserId == selected.Id;

        SetActionState(
            SaveUserButton,
            canWriteUsers,
            canWriteUsers ? null : WritePermissionHint);

        SetActionState(
            ActivateUserButton,
            canWriteUsers && selected is { IsActive: false },
            !canWriteUsers ? WritePermissionHint
                : selected is null ? SelectionHint
                : selected.IsActive ? "Ce compte est déjà actif."
                : null);

        SetActionState(
            DeactivateUserButton,
            canWriteUsers && selected is { IsActive: true } && !isSelf,
            !canWriteUsers ? WritePermissionHint
                : selected is null ? SelectionHint
                : isSelf ? SelfDeactivationHint
                : !selected.IsActive ? "Ce compte est déjà désactivé."
                : null);

        SetActionState(
            UnlockUserButton,
            canWriteUsers && selected is { IsLockedOut: true },
            !canWriteUsers ? WritePermissionHint
                : selected is null ? SelectionHint
                : !selected.IsLockedOut ? "Ce compte n'est pas sous verrouillage : il n'y a rien à lever."
                : null);

        SetActionState(
            ResetPasswordButton,
            canWriteUsers && selected is not null,
            !canWriteUsers ? WritePermissionHint
                : selected is null ? SelectionHint
                : null);

        SetActionState(
            SaveRolesButton,
            canWriteUsers && selected is not null && roleCatalog.Count > 0,
            !canWriteUsers ? WritePermissionHint
                : selected is null ? "En création, les rôles cochés sont accordés au moment où le compte est créé : il n'y a rien à enregistrer séparément."
                : roleCatalog.Count == 0 ? "Aucun rôle n'est défini sur le serveur."
                : null);
    }

    // Pose le message d'explication quand l'action est grisee, et RESTAURE
    // l'info-bulle d'origine du bouton des qu'elle redevient possible.
    private void SetActionState(Button button, bool enabled, string? reason)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.IsEnabled = enabled;
        button.ToolTip = reason ?? originalToolTips[button];
    }

    // ============================== Outils ==============================

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

    private static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    // Projection d'affichage d'une ligne de la grille. Les noms de roles sont
    // traduits une seule fois ici, et le statut est reduit a une cle unique
    // ("Active", "Inactive", "Locked") qui pilote la pastille du XAML.
    private UserRowView ToRowView(UserAccountResponse user)
    {
        // Un compte desactive l'emporte sur un verrouillage : il ne peut de toute
        // facon plus se connecter, et proposer de "deverrouiller" serait trompeur.
        var statusKind = !user.IsActive ? "Inactive" : user.IsLockedOut ? "Locked" : "Active";

        var statusTooltip = statusKind switch
        {
            "Inactive" => "Compte désactivé : il ne peut plus se connecter.",
            "Locked" => user.LockedOutUntil is DateTimeOffset until
                ? $"Verrouillé après plusieurs échecs de connexion, jusqu'au {FormatMoment(until)}."
                : "Verrouillé après plusieurs échecs de connexion.",
            _ => "Compte actif."
        };

        return new UserRowView(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.Roles.Count == 0 ? "Aucun rôle" : string.Join(", ", user.Roles.Select(DescribeRole)),
            statusKind,
            statusTooltip,
            user.MustChangePassword ? "Oui" : "—",
            user.LastLoginAt is DateTimeOffset lastLogin ? FormatMoment(lastLogin) : "Jamais",
            user.IsActive,
            user.IsLockedOut,
            user.Roles);
    }

    // Case a cocher du selecteur de roles. Classe (et non record) parce que
    // IsSelected est ecrit par la liaison bidirectionnelle de la case a cocher.
    private sealed class RoleOptionView(string name, string displayName, string description, bool isSelected)
    {
        public string Name { get; } = name;

        public string DisplayName { get; } = displayName;

        public string Description { get; } = description;

        public bool IsSelected { get; set; } = isSelected;
    }

    private sealed record UserRowView(
        Guid Id,
        string UserName,
        string DisplayName,
        string Email,
        string RolesText,
        string StatusKind,
        string StatusTooltip,
        string MustChangePasswordText,
        string LastLoginText,
        bool IsActive,
        bool IsLockedOut,
        IReadOnlyCollection<string> Roles);
}
