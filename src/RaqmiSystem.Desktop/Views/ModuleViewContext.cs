using RaqmiSystem.Desktop.Api;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Ce que la fenetre principale prete a une vue de module : le client API, l'URL
/// courante du serveur, et les deux services transverses (message d'etat, execution
/// d'un appel API avec curseur d'attente et traduction des erreurs).
///
/// Les vues de module sont des UserControl autonomes : elles ne connaissent ni
/// MainWindow ni les autres vues, elles recoivent ce contexte via Initialize().
/// </summary>
public sealed class ModuleViewContext(
    RaqmiApiClient apiClient,
    Func<string> apiBaseUrlProvider,
    Action<string, bool> setStatus,
    Func<Func<Task>, Task> runApiActionAsync,
    Func<string, bool> hasPermission)
{
    public RaqmiApiClient ApiClient { get; } = apiClient;

    /// <summary>URL du serveur telle que saisie sur l'ecran de connexion.</summary>
    public string ApiBaseUrl => apiBaseUrlProvider();

    /// <summary>
    /// Le profil connecte detient-il cette permission (cle de PermissionCatalog) ?
    /// A utiliser pour DESACTIVER les actions d'ecriture qu'un utilisateur en
    /// lecture seule ne peut pas effectuer, plutot que de le laisser decouvrir
    /// l'interdiction sur un 403 apres avoir saisi tout un formulaire.
    /// L'autorisation reste evidemment appliquee par le serveur : ceci n'est
    /// qu'un confort d'interface, jamais une mesure de securite.
    /// </summary>
    public bool HasPermission(string permission) => hasPermission(permission);

    /// <summary>Affiche un message dans le bandeau de session de la fenetre.</summary>
    public void SetStatus(string message, bool isError = false) => setStatus(message, isError);

    /// <summary>
    /// Execute un appel API : curseur d'attente, barre de progression, et
    /// traduction des erreurs (HTTP, reseau, validation) en message d'etat.
    /// Toute action declenchee par un bouton d'une vue de module doit passer par la.
    /// </summary>
    public Task RunAsync(Func<Task> action) => runApiActionAsync(action);
}
