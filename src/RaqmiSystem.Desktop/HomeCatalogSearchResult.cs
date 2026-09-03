using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Un resultat de la recherche universelle du catalogue : un ecran ou un sous-module
/// planifie de l'arbre, avec son chemin complet.
/// </summary>
/// <remarks>
/// La recherche du catalogue cherche des ECRANS, pas des donnees. Elle descend au niveau
/// que les cartes ne montrent pas - « night audit » et « balance âgée » sont des ecrans,
/// pas des modules - et elle montre aussi les noeuds planifies, avec leur badge : le
/// catalogue promet ce qui existe et annonce ce qui n'existe pas encore, il ne cache ni
/// l'un ni l'autre.
///
/// Un ecran que le profil ne peut pas lire reste listé, cadenassé : sur l'accueil on
/// montre tout, cadenas compris (decision 7 du README) ; c'est la barre laterale qui
/// elague. Le masquage n'est de toute facon jamais une securite.
/// </remarks>
public sealed record HomeCatalogSearchResult(
    string Label,
    string Path,
    string IconKey,
    string MaturityLabel,
    FunctionalMaturity Maturity,
    int? TabIndex,
    bool IsLocked)
{
    /// <summary>Ouvrable = un ecran existe ET le profil a le droit de le lire.</summary>
    public bool IsOpenable => TabIndex is not null && !IsLocked;

    /// <summary>
    /// Un ecran existe derriere ce resultat. Faux = noeud planifie de l'arbre : le SEUL cas
    /// ou le badge de maturite s'affiche.
    /// </summary>
    /// <remarks>
    /// Les deux echelles ne se melangent pas : la maturite est une propriete du DOMAINE
    /// (navigation-shell § 6.3), l'ouvrabilite est une propriete du profil. Peindre « Écran »
    /// ou « Accès refusé » dans la teinte de maturite ferait porter au vert « Fonctionnel »
    /// deux mots opposes sur la meme liste - la couleur ne dirait plus rien.
    /// </remarks>
    public bool HasScreen => TabIndex is not null;

    /// <summary>Le chemin, ou le refus quand l'ecran n'est pas ouvrable pour ce profil.</summary>
    public string ToolTipText => IsLocked
        ? ModuleTile.AccessDeniedToolTip
        : TabIndex is null
            ? $"{Path} · {MaturityLabel} · aucun écran"
            : Path;

    public string AccessibleName => TabIndex is null
        ? $"{Label}, {Path}, {MaturityLabel}, aucun écran"
        : IsLocked
            ? $"{Label}, {Path}, {ModuleTile.AccessDeniedToolTip.ToLowerInvariant()}"
            : $"Ouvrir {Label}, {Path}";

    /// <summary>
    /// Les resultats d'une recherche, dans l'ordre de l'arbre : les ecrans d'abord, les
    /// noeuds planifies ensuite. L'arbre recu est celui construit avec TOUTES les cles
    /// (les cadenas sont poses ici, en comparant aux cles du profil) et avec les alias :
    /// « circuits » doit trouver les validations meme par leur chemin secondaire.
    /// </summary>
    public static IReadOnlyList<HomeCatalogSearchResult> From(NavigationTree tree, IReadOnlySet<string> grantedKeys)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(grantedKeys);

        var screens = new List<HomeCatalogSearchResult>();
        var planned = new List<HomeCatalogSearchResult>();

        foreach (var domain in tree.Domains)
        {
            foreach (var module in domain.Modules)
            {
                foreach (var submodule in module.Submodules)
                {
                    var path = $"{domain.Id} {domain.Label} › {module.Label}";

                    if (submodule.Screens.Count == 0)
                    {
                        planned.Add(new HomeCatalogSearchResult(
                            submodule.Label,
                            path,
                            domain.IconKey,
                            FunctionalMaturityMapper.Label(submodule.Maturity),
                            submodule.Maturity,
                            TabIndex: null,
                            IsLocked: false));

                        continue;
                    }

                    foreach (var screen in submodule.Screens)
                    {
                        screens.Add(new HomeCatalogSearchResult(
                            screen.Label,
                            $"{path} › {submodule.Label}",
                            domain.IconKey,
                            FunctionalMaturityMapper.Label(screen.Maturity),
                            screen.Maturity,
                            screen.LegacyTabIndex,
                            IsLocked: !grantedKeys.Contains(screen.ReadPermissionKey)));
                    }
                }
            }
        }

        return [.. screens, .. planned];
    }
}
