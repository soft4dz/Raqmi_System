namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Périmètre de visibilité d'un nœud de navigation.
/// </summary>
/// <remarks>
/// Simple réservation de place : l'identité ne porte aujourd'hui aucune affectation
/// utilisateur ↔ unité (claim <c>permission</c> seulement), donc aucun filtrage réel n'est
/// possible. Le champ existe pour que l'arbre n'ait pas à changer de forme le jour où le
/// modèle d'affectation arrive (plan de migration, phase 2).
/// </remarks>
public enum NavigationScope
{
    Global,
    Unit
}

/// <summary>
/// Ce que tout nœud de l'arbre expose, du domaine à l'écran.
/// </summary>
/// <remarks>
/// <see cref="ReadPermissionKey"/> n'est exigée que sur un écran : un sous-module, un module
/// ou un domaine est visible dès qu'un de ses enfants l'est, il ne porte donc pas de clé
/// propre. <see cref="LicenseFeature"/> est nul partout aujourd'hui, aucune licence n'étant
/// modélisée ; le champ est là pour que le filtre de licence ait quelque chose à lire.
/// </remarks>
public interface INavigationNode
{
    /// <summary>Identifiant stable, kebab-case ASCII, hiérarchique (« 06.front-office.arrivals »).</summary>
    string Id { get; }

    string Label { get; }

    /// <summary>Rang parmi les frères, à partir de 1.</summary>
    int Order { get; }

    string IconKey { get; }

    string? ReadPermissionKey { get; }

    FunctionalMaturity Maturity { get; }

    string? LicenseFeature { get; }

    NavigationScope Scope { get; }
}

/// <summary>
/// Feuille de l'arbre : un écran ouvrable, ou un chemin secondaire vers cet écran.
/// </summary>
/// <remarks>
/// Un même onglet peut être atteint par plusieurs chemins (les validations depuis Mon Espace
/// et depuis le paramétrage des circuits). Un seul chemin est primaire ; les autres portent
/// <see cref="IsAlias"/> et ne créent jamais de doublon dans la barre latérale.
/// <see cref="LegacyTabIndex"/> et <see cref="LegacyOrder"/> sont l'adaptateur de
/// compatibilité vers <c>MainTabs</c> et le catalogue historique : ils ne sont pas
/// l'identifiant cible.
/// </remarks>
public sealed record ScreenNode(
    string Id,
    string Label,
    int Order,
    string IconKey,
    string ReadPermissionKey,
    FunctionalMaturity Maturity,
    string? LicenseFeature,
    NavigationScope Scope,
    int? LegacyTabIndex,
    string? LegacyOrder,
    bool IsAlias,
    string? Description) : INavigationNode
{
    /// <summary>Un écran sans onglet n'existe pas encore : il ne peut pas être ouvert.</summary>
    public bool IsOpenable => LegacyTabIndex is not null;
}

/// <summary>
/// Sous-module : le niveau le plus fin de la cartographie cible. Sans écran, il est un nœud
/// <see cref="FunctionalMaturity.Planned"/> : visible sur l'accueil, jamais ouvrable.
/// </summary>
public sealed record SubmoduleNode(
    string Id,
    string Label,
    int Order,
    string IconKey,
    string? ReadPermissionKey,
    FunctionalMaturity Maturity,
    string? LicenseFeature,
    NavigationScope Scope,
    IReadOnlyList<ScreenNode> Screens) : INavigationNode;

/// <summary>
/// Module : ce que la barre latérale affiche sous un domaine.
/// </summary>
/// <remarks>
/// <see cref="LegacyModuleOrders"/> liste les entrées du catalogue historique que ce module
/// absorbe sur l'accueil (planifiées comprises). C'est le complément, au niveau module, du
/// rattachement par domaine de <see cref="FunctionalArchitectureCatalog.DomainForLegacyOrder"/>.
/// </remarks>
public sealed record ModuleNode(
    string Id,
    string Label,
    int Order,
    string IconKey,
    string? ReadPermissionKey,
    FunctionalMaturity Maturity,
    string? LicenseFeature,
    NavigationScope Scope,
    IReadOnlyList<string> LegacyModuleOrders,
    IReadOnlyList<SubmoduleNode> Submodules) : INavigationNode;

/// <summary>
/// Domaine : l'un des 22 espaces de travail de l'architecture cible.
/// </summary>
public sealed record DomainNode(
    string Id,
    string Label,
    int Order,
    string IconKey,
    string? ReadPermissionKey,
    FunctionalMaturity Maturity,
    string? LicenseFeature,
    NavigationScope Scope,
    IReadOnlyList<ModuleNode> Modules) : INavigationNode;

/// <summary>
/// Chemin complet vers un écran : ce que le fil d'Ariane affiche.
/// </summary>
public sealed record NavigationPath(
    DomainNode Domain,
    ModuleNode Module,
    SubmoduleNode Submodule,
    ScreenNode Screen)
{
    public IReadOnlyList<string> Labels => [Domain.Label, Module.Label, Submodule.Label, Screen.Label];
}

/// <summary>
/// Place d'une entrée du catalogue historique dans l'arbre cible : son domaine, son module
/// et le rang du module dans le parcours de l'arbre, pour ordonner l'accueil.
/// </summary>
public sealed record NavigationModulePlacement(DomainNode Domain, ModuleNode Module, int ModuleRank);
