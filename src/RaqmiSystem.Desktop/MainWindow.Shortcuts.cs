using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Desktop.Views;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Raccourcis clavier de la fenetre principale.
///
/// Pourquoi : les ecrans de ce produit sont des ecrans de saisie tenus a la journee -
/// une reception, une comptabilite, un economat. Ceux qui y travaillent ne lachent pas
/// le clavier pour aller chercher « Actualiser » a la souris quarante fois par jour.
///
/// Deux familles :
///   - les raccourcis de NAVIGATION, traites ici meme (aller a un module, accueil,
///     module suivant / precedent, aide) ;
///   - les raccourcis d'ECRAN, delegues a <see cref="ShortcutRouter"/>, qui trouve dans
///     l'ecran affiche le bouton correspondant et le declenche comme un clic.
///
/// Rien ne contourne les permissions ni l'etat d'attente : le routeur n'actionne qu'un
/// controle que l'utilisateur pourrait cliquer lui-meme a cet instant. Un raccourci sans
/// cible ne fait rien, et le dit dans le bandeau de session plutot que de rester muet.
/// </summary>
public partial class MainWindow
{
    // Aucun raccourci avant l'ouverture de session : l'ecran de connexion a ses propres
    // touches (Entree pour valider), et Ctrl+S sur une carte de connexion n'a pas de sens.
    private bool ShortcutsAvailable => MainContentGrid.Visibility == Visibility.Visible;

    // Racine de recherche des raccourcis d'ecran : le conteneur d'onglets, jamais la
    // fenetre entiere. Un raccourci ne doit pas atteindre l'en-tete ni la barre laterale.
    private DependencyObject CurrentScreenRoot => MainTabs;

    // ============================ Raccourcis d'ecran ============================

    private void RefreshShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        InvokeOrExplain(
            ShortcutRouter.FindRefreshButton(CurrentScreenRoot),
            "Cet écran n'a rien à actualiser.");
    }

    private void SaveShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        InvokeOrExplain(
            ShortcutRouter.FindSaveButton(CurrentScreenRoot),
            "Aucun formulaire à enregistrer ici.");
    }

    private void NewShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        InvokeOrExplain(
            ShortcutRouter.FindNewButton(CurrentScreenRoot),
            "Cet écran ne permet pas de créer un élément.");
    }

    private void FindShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ShortcutsAvailable)
        {
            return;
        }

        if (ShortcutRouter.FindSearchBox(CurrentScreenRoot) is not { } box)
        {
            SetStatus("Cet écran n'a pas de champ de recherche.");
            return;
        }

        box.Focus();
        box.SelectAll();
    }

    // Un bouton grise par une permission manquante n'est pas trouve par le routeur : le
    // message tombe alors dans le cas « pas de cible ». C'est volontaire - l'utilisateur
    // apprend que l'action n'existe pas pour lui, exactement comme le bouton grise le lui
    // dit deja a l'ecran, sans que le raccourci ouvre une voie que le clic n'ouvre pas.
    private void InvokeOrExplain(Button? target, string absenceMessage)
    {
        if (!ShortcutsAvailable)
        {
            return;
        }

        if (target is null)
        {
            SetStatus(absenceMessage);
            return;
        }

        target.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    // ========================== Raccourcis de navigation ==========================

    // Ctrl+K : aller a un module. Le champ de recherche de la barre laterale filtre les
    // modules sur leur nom, leur description et leur famille ; le focus y est mis avec
    // la saisie precedente selectionnee, pour que taper remplace au lieu d'ajouter.
    private void GoToModuleShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ShortcutsAvailable)
        {
            return;
        }

        // Sur l'accueil la barre laterale est repliee par design (la racine EST le
        // sommaire) : c'est la recherche du catalogue qui joue le meme role.
        if (MainTabs.SelectedIndex == HomeTabIndex)
        {
            ModuleCatalogView.FocusSearch();
            return;
        }

        ModuleSearchTextBox.Focus();
        ModuleSearchTextBox.SelectAll();
    }

    private void GoHomeShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (ShortcutsAvailable)
        {
            NavigateToModule(HomeTabIndex);
        }
    }

    private void NextModuleShortcut_Executed(object sender, ExecutedRoutedEventArgs e) =>
        MoveModule(1);

    private void PreviousModuleShortcut_Executed(object sender, ExecutedRoutedEventArgs e) =>
        MoveModule(-1);

    // Passe au module ouvrable suivant, dans l'ordre de l'arbre visible - celui de la
    // barre laterale, pas celui des onglets, qui n'est que l'ordre d'ajout des ecrans.
    // La liste boucle : depuis le dernier ecran on revient a l'accueil, qui n'est garde
    // par aucune permission et sert de point fixe. CanOpenModule reste le garde final :
    // l'ordre est calcule des permissions, il ne les remplace pas.
    private void MoveModule(int direction)
    {
        if (!ShortcutsAvailable)
        {
            return;
        }

        var target = NavigationKeyboardOrder.Next(
            navigableTree.OpenableTabOrder,
            HomeTabIndex,
            MainTabs.SelectedIndex,
            direction);

        if (CanOpenModule(target))
        {
            NavigateToModule(target);
        }
    }

    private void ShortcutHelpShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var window = new ShortcutsWindow { Owner = this };
        window.ShowDialog();
    }

    // ============================== Apparence ==============================

    // Bascule clair / sombre. Le mode « Systeme » est le reglage de depart, tant que
    // personne n'a tranche sur ce poste ; le premier clic fige un choix explicite, et
    // l'ecran de parametrage permet de revenir a « Systeme ».
    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var versLeSombre = !EstActuellementSombre();
        var mode = versLeSombre ? ApparenceMode.Sombre : ApparenceMode.Clair;

        ThemeManager.Appliquer(System.Windows.Application.Current.Resources, mode);
        DesktopSettings.SaveApparence(mode);
        SyncThemeToggle();

        // Les ecrans deja ouverts gardent leur apparence : leurs couleurs ont ete resolues
        // une fois pour toutes (voir ThemeManager). Le dire vaut mieux que laisser croire
        // que la bascule n'a pas fonctionne.
        var applique = versLeSombre ? "Apparence sombre activée." : "Apparence claire activée.";

        SetStatus(ThemeManager.RedemarrageConseille
            ? applique + " Les écrans déjà ouverts la prendront au prochain démarrage."
            : applique);
    }

    private bool EstActuellementSombre() => ThemeManager.ModeApplique switch
    {
        ApparenceMode.Sombre => true,
        ApparenceMode.Clair => false,
        _ => ThemeManager.WindowsPrefereLeSombre(),
    };

    // L'icone annonce ce que le clic va faire, pas ou l'on est : sur fond clair on montre
    // une lune (« passer en sombre »), sur fond sombre un soleil. L'info-bulle le repete en
    // toutes lettres, parce qu'une icone seule laisse toujours planer le doute.
    /// <summary>
    /// Realigne l'icone de bascule sur le theme courant. Public parce que l'ecran de
    /// parametrage change lui aussi l'apparence, et que l'en-tete doit alors suivre.
    /// </summary>
    public void RefreshApparenceToggle() => SyncThemeToggle();

    private void SyncThemeToggle()
    {
        var sombre = EstActuellementSombre();

        ThemeToggleMoonIcon.Visibility = sombre ? Visibility.Collapsed : Visibility.Visible;
        ThemeToggleSunIcon.Visibility = sombre ? Visibility.Visible : Visibility.Collapsed;
        ThemeToggleButton.ToolTip = sombre
            ? "Passer à l'apparence claire"
            : "Passer à l'apparence sombre";

        System.Windows.Automation.AutomationProperties.SetName(
            ThemeToggleButton,
            (string)ThemeToggleButton.ToolTip);
    }
}
