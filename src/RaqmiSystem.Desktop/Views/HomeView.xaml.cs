using System.Windows.Controls;
using RaqmiSystem.Application.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Hote de l'onglet 0 « Mon Espace » : deux sections, « Mon travail » et
/// « Catalogue des modules ».
/// </summary>
/// <remarks>
/// L'hote ne contient aucune logique propre : il relaie. Les deux sections sont
/// autonomes, et la fenetre ne parle qu'a lui - un seul point de contact pour la
/// connexion, la deconnexion, le rafraichissement et la navigation.
/// </remarks>
public partial class HomeView : UserControl
{
    private const int WorkSectionIndex = 0;
    private const int CatalogSectionIndex = 1;

    public HomeView()
    {
        InitializeComponent();

        WorkQueuesView.NavigateRequested += tab => NavigateRequested?.Invoke(tab);
        WorkQueuesView.ChangePasswordRequested += () => ChangePasswordRequested?.Invoke();
        WorkQueuesView.OpenCatalogRequested += ShowCatalogSection;
        ModuleCatalogView.NavigateRequested += tab => NavigateRequested?.Invoke(tab);
    }

    /// <summary>Ouverture d'un ecran demandee par l'une des deux sections.</summary>
    public event Action<int>? NavigateRequested;

    /// <summary>« Ma sécurité » : la fenetre ouvre sa boite de changement de mot de passe.</summary>
    public event Action? ChangePasswordRequested;

    /// <summary>
    /// Ce que la fenetre sait des le demarrage, avant toute session : les 50 tuiles, les
    /// cles courantes (toutes hors session) et le garde de navigation. Aucun reseau.
    /// </summary>
    public void InitializeCatalog(
        IReadOnlyList<ModuleTile> tiles,
        Func<IReadOnlySet<string>> grantedKeys,
        Func<int, bool> canOpenModule)
    {
        ModuleCatalogView.Initialize(tiles, grantedKeys);
        WorkQueuesView.InitializeNavigation(grantedKeys, canOpenModule);
    }

    /// <summary>Contrat § 2.1 : le contexte de session arrive a la connexion, sans appel reseau.</summary>
    public void Initialize(ModuleViewContext context) => WorkQueuesView.Initialize(context);

    public void OpenSession(AuthenticatedUser user) => WorkQueuesView.OpenSession(user);

    public Task LoadAsync() => WorkQueuesView.LoadAsync();

    public Task RefreshIfStaleAsync() => WorkQueuesView.RefreshIfStaleAsync();

    /// <summary>
    /// L'unite de ce poste a change dans Parametrage global : les files unitaires en
    /// dependent, la prochaine venue sur l'onglet 0 recharge sans attendre cinq minutes.
    /// </summary>
    public void InvalidateWorkQueues() => WorkQueuesView.Invalidate();

    public void ResetState()
    {
        WorkQueuesView.ResetState();
        ShowWorkSection();
    }

    public void RecordVisit(int tabIndex) => WorkQueuesView.RecordVisit(tabIndex);

    /// <summary>Les cadenas du catalogue ont bouge : ses resultats de recherche aussi.</summary>
    public void RefreshPermissions() => ModuleCatalogView.RefreshPermissions();

    /// <summary>Ctrl+K : la recherche vit dans le catalogue, on y bascule avant de la focaliser.</summary>
    public void FocusCatalogSearch()
    {
        ShowCatalogSection();
        ModuleCatalogView.FocusSearch();
    }

    /// <summary>Alt+Origine revient a l'accueil ET a sa premiere section.</summary>
    public void ShowWorkSection() => SectionTabControl.SelectedIndex = WorkSectionIndex;

    private void ShowCatalogSection() => SectionTabControl.SelectedIndex = CatalogSectionIndex;
}
