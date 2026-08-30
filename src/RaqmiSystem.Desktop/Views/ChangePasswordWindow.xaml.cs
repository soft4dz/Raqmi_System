using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Desktop.Api;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Changement de mot de passe en libre-service (POST /api/v1/account/change-password).
///
/// La fenetre porte son propre appel API plutot que de passer par le
/// <see cref="ModuleViewContext"/> des vues de module : elle est modale, donc le
/// bandeau de session de la fenetre principale est hors de vue pendant l'appel, et
/// un message d'erreur affiche la-bas serait invisible au moment ou il compte.
///
/// Elle s'ouvre dans deux situations :
///   - a la demande, depuis le bouton de l'en-tete ;
///   - automatiquement apres une connexion dont la reponse porte
///     <c>MustChangePassword</c>. C'est ce second cas qui rend le drapeau utile :
///     sans lui, un administrateur le pose en remettant un mot de passe temporaire
///     et rien ne vient jamais le lever.
/// </summary>
public partial class ChangePasswordWindow : Window
{
    private readonly RaqmiApiClient apiClient;
    private readonly string apiBaseUrl;

    private bool isBusy;

    /// <summary>
    /// Nombre de sessions fermees par le changement, tel que le serveur l'a compte.
    /// Renseigne uniquement quand <see cref="Window.DialogResult"/> vaut true.
    /// </summary>
    public int RevokedSessionCount { get; private set; }

    /// <param name="isMandatory">
    /// Vrai quand la connexion a signale que le mot de passe DOIT etre change. Seul
    /// le discours change : le bandeau d'explication apparait et le bouton de sortie
    /// devient "Plus tard". La fenetre reste refermable - enfermer quelqu'un dans une
    /// modale sans issue le priverait aussi du bouton de deconnexion, et le drapeau
    /// est de toute facon reapplique a la connexion suivante.
    /// </param>
    public ChangePasswordWindow(RaqmiApiClient apiClient, string apiBaseUrl, bool isMandatory)
    {
        InitializeComponent();

        this.apiClient = apiClient;
        this.apiBaseUrl = apiBaseUrl;

        // Le seuil affiche est celui que le serveur applique : la constante est lue,
        // jamais recopiee, pour que l'ecran ne puisse pas annoncer une regle que le
        // serveur ne suit plus.
        LengthHintTextBlock.Text =
            $"Au moins {PasswordPolicy.MinimumLength.ToString(CultureInfo.CurrentCulture)} caractères, " +
            $"et différent de votre mot de passe actuel.";

        if (isMandatory)
        {
            MandatoryNoticeBorder.Visibility = Visibility.Visible;
            SubtitleTextBlock.Text = "Votre compte utilise encore un mot de passe temporaire.";
            CancelButton.Content = "Plus tard";
            CancelButton.ToolTip = "Fermer sans changer le mot de passe. Le rappel réapparaîtra à la prochaine connexion.";
        }

        Loaded += (_, _) => CurrentPasswordBox.Focus();
    }

    // Efface le message d'erreur des que l'utilisateur corrige sa saisie : garder
    // "les deux saisies different" a l'ecran pendant qu'il retape la confirmation
    // ferait lire un reproche deja obsolete.
    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (StatusBorder.Visibility == Visibility.Visible && !isBusy)
        {
            HideStatus();
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy)
        {
            return;
        }

        var currentPassword = CurrentPasswordBox.Password;
        var newPassword = NewPasswordBox.Password;
        var confirmation = ConfirmPasswordBox.Password;

        if (string.IsNullOrEmpty(currentPassword))
        {
            ShowStatus("Saisissez votre mot de passe actuel.", isError: true);
            CurrentPasswordBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowStatus("Le nouveau mot de passe ne peut pas être vide.", isError: true);
            NewPasswordBox.Focus();
            return;
        }

        // Meme seuil que le serveur, lu dans la meme constante. Le verifier ici evite
        // un aller-retour reseau pour une erreur que le poste connaissait deja.
        if (newPassword.Length < PasswordPolicy.MinimumLength)
        {
            ShowStatus(
                $"Le nouveau mot de passe doit compter au moins {PasswordPolicy.MinimumLength.ToString(CultureInfo.CurrentCulture)} caractères.",
                isError: true);
            NewPasswordBox.Focus();
            return;
        }

        if (newPassword.Length > PasswordPolicy.MaximumLength)
        {
            ShowStatus(
                $"Le nouveau mot de passe ne peut pas dépasser {PasswordPolicy.MaximumLength.ToString(CultureInfo.CurrentCulture)} caractères.",
                isError: true);
            NewPasswordBox.Focus();
            return;
        }

        // Comparaison ordinale, comme cote serveur : deux chaines qui ne different
        // que par leur forme de normalisation Unicode sont deux mots de passe
        // differents pour la derivation de cle, donc pour ces controles aussi.
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            ShowStatus("Les deux saisies du nouveau mot de passe ne correspondent pas.", isError: true);
            ConfirmPasswordBox.Clear();
            ConfirmPasswordBox.Focus();
            return;
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            ShowStatus("Le nouveau mot de passe doit être différent de l'actuel.", isError: true);
            NewPasswordBox.Focus();
            return;
        }

        SetBusy(true);

        try
        {
            var result = await apiClient.ChangePasswordAsync(
                apiBaseUrl,
                new ChangePasswordRequest(currentPassword, newPassword));

            RevokedSessionCount = result.RevokedSessionCount;
            DialogResult = true;
        }
        catch (ApiRequestFailedException ex)
        {
            ShowStatus($"API {((int)ex.StatusCode).ToString(CultureInfo.CurrentCulture)} : {ex.Message}", isError: true);
        }
        catch (HttpRequestException ex)
        {
            ShowStatus($"API indisponible : {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;

        Cursor = busy ? Cursors.Wait : null;
        BusyProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SubmitButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        CurrentPasswordBox.IsEnabled = !busy;
        NewPasswordBox.IsEnabled = !busy;
        ConfirmPasswordBox.IsEnabled = !busy;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusBorder.Tag = isError ? "Error" : "Info";
        StatusBorder.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusTextBlock.Text = string.Empty;
        StatusBorder.Visibility = Visibility.Collapsed;
    }
}
