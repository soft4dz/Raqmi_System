using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Applique la palette claire ou sombre au dictionnaire de ressources.
///
/// COMMENT, et pourquoi pas autrement.
///
/// Les 3 064 references de couleur du client sont des <c>{StaticResource …}</c>. Une
/// premiere approche consistait a changer la propriete <c>Color</c> des brushes deja
/// resolus : StaticResource capture la reference de l'objet, donc muter l'objet aurait
/// repeint tout, partout, a chaud. Cette approche ne marche pas ici, et l'echec est net :
/// <c>InvalidOperationException — impossible de definir une propriete pour l'objet
/// '#FF073B78', car il est en lecture seule</c>.
///
/// La raison : WPF SCELLE les Freezable places dans un <see cref="ResourceDictionary"/>
/// rattache a l'<see cref="System.Windows.Application"/>, pour les rendre partageables
/// entre threads. Le gel ne vient pas du XAML (le theme n'ecrit aucun <c>Freeze</c>) mais
/// de ce rattachement, et il frappe aussi les copies qu'on tenterait de reinjecter :
/// cloner puis reposer le clone dans le dictionnaire le regele aussitot.
///
/// La methode retenue REMPLACE l'entree du dictionnaire par un brush neuf, au lieu de
/// modifier l'objet en place. Consequence a connaitre : une reference
/// <c>{StaticResource}</c> DEJA resolue garde l'ancien objet. C'est sans effet au
/// demarrage, ou rien n'est encore resolu - le theme choisi s'applique donc entierement.
/// En revanche, un changement en cours de session ne repeint que ce qui n'a pas encore
/// ete affiche ; les ecrans deja ouverts gardent leur apparence jusqu'au redemarrage, et
/// l'interface le dit plutot que de laisser croire a un bug.
///
/// Repeindre a chaud demanderait de convertir les references de couleur en
/// <c>{DynamicResource}</c> - environ 3 000 lignes de XAML. C'est faisable et mecanique,
/// mais c'est un chantier a part, pas un effet de bord de la palette.
///
/// Deux ressources restent hors de portee, sans consequence :
///   - <c>StructureShadowColor</c> est une <c>Color</c>, une valeur et non un objet. Elle
///     vaut #071525, une ombre presque noire, juste sur fond sombre - ou les ombres
///     s'effacent de toute facon devant les bordures ;
///   - <c>LoginBackdropBrush</c> habille l'ecran de connexion, deja sombre dans les deux
///     themes : c'est la scene de marque, elle ne change pas.
/// </summary>
internal static class ThemeManager
{
    private const string PersonalisationKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    // Palette claire relevee au demarrage, avant toute bascule : revenir au clair consiste
    // a la reposer. La relever plutot que de la reecrire a la main garantit qu'elle ne
    // derive jamais de ce que le theme contient vraiment.
    //
    // Le dictionnaire porteur est memorise avec la clef : le theme est charge par FUSION
    // (App.xaml), et ResourceDictionary.Keys n'enumere que les clefs propres du
    // dictionnaire interroge, jamais celles de ses MergedDictionaries.
    private static readonly List<(ResourceDictionary Porteur, string Clef, Color Claire)> Couleurs = [];

    private static bool prete;

    /// <summary>Theme effectivement applique.</summary>
    public static ApparenceMode ModeApplique { get; private set; } = ApparenceMode.Clair;

    /// <summary>Densite effectivement appliquee.</summary>
    public static ApparenceDensite DensiteAppliquee { get; private set; } = ApparenceDensite.Confortable;

    /// <summary>
    /// Vrai si le theme a change depuis le demarrage. Les ecrans deja affiches gardent alors
    /// l'ancienne apparence : c'est ce que l'interface doit annoncer.
    /// </summary>
    public static bool RedemarrageConseille { get; private set; }

    /// <summary>
    /// Releve la palette claire. A appeler une fois, au demarrage, avant l'affichage de la
    /// premiere fenetre.
    /// </summary>
    public static void Prepare(ResourceDictionary ressources)
    {
        if (prete)
        {
            return;
        }

        foreach (var dictionnaire in Aplatir(ressources))
        {
            // On n'interroge que les clefs de la palette sombre, jamais tout le dictionnaire :
            // lire une clef force la realisation de la ressource, et realiser les styles dans
            // un ordre arbitraire pendant le demarrage n'a aucune raison d'etre sain.
            foreach (var clef in ThemePalette.Sombre.Keys)
            {
                if (!dictionnaire.Contains(clef) || dictionnaire[clef] is not SolidColorBrush brush)
                {
                    continue;
                }

                Couleurs.Add((dictionnaire, clef, brush.Color));
            }
        }

        VerifierCouverture();
        prete = true;
    }

    /// <summary>Applique un mode. <see cref="ApparenceMode.Systeme"/> suit le reglage Windows.</summary>
    public static void Appliquer(ResourceDictionary ressources, ApparenceMode mode)
    {
        Prepare(ressources);

        var sombre = mode switch
        {
            ApparenceMode.Sombre => true,
            ApparenceMode.Clair => false,
            _ => WindowsPrefereLeSombre(),
        };

        var etaitSombre = ModeApplique switch
        {
            ApparenceMode.Sombre => true,
            ApparenceMode.Clair => false,
            _ => WindowsPrefereLeSombre(),
        };

        foreach (var (porteur, clef, couleurClaire) in Couleurs)
        {
            var couleur = sombre && ThemePalette.Sombre.TryGetValue(clef, out var hex)
                ? Lire(hex, couleurClaire)
                : couleurClaire;

            // Remplacement de l'entree, et non modification du brush : voir l'en-tete de
            // classe. Le brush neuf sera scelle a son tour par le dictionnaire, ce qui est
            // sans importance puisqu'on ne le modifiera plus jamais.
            porteur[clef] = new SolidColorBrush(couleur);
        }

        // Le premier passage est celui du demarrage : il ne conseille aucun redemarrage,
        // puisque rien n'a encore ete affiche.
        RedemarrageConseille |= prete && ModeApplique != mode && etaitSombre != sombre && Affichee;
        ModeApplique = mode;
    }

    /// <summary>
    /// Applique une densite de grille. Les deux clefs sont lues en <c>{DynamicResource}</c>
    /// par les styles de <c>DataGrid</c> : les remplacer repeint les 759 grilles du produit,
    /// y compris celles deja affichees. C'est justement ce que StaticResource ne permet pas,
    /// et la raison pour laquelle ces deux-la sont dynamiques.
    /// </summary>
    public static void AppliquerDensite(ResourceDictionary ressources, ApparenceDensite densite)
    {
        var compact = densite == ApparenceDensite.Compact;

        // Posees sur le dictionnaire qui les declare, pas sur la racine : ajouter la clef a
        // la racine masquerait celle du theme au lieu de la remplacer.
        foreach (var dictionnaire in Aplatir(ressources))
        {
            if (!dictionnaire.Contains("GridRowHeight"))
            {
                continue;
            }

            dictionnaire["GridRowHeight"] = compact ? 32d : 40d;
            dictionnaire["GridHeaderHeight"] = compact ? 34d : 40d;
        }

        DensiteAppliquee = densite;
    }

    /// <summary>
    /// Windows demande-t-il le theme sombre pour les applications ? La valeur absente vaut
    /// clair : c'est le defaut de Windows, et un poste sans cette clef n'a jamais choisi.
    /// </summary>
    public static bool WindowsPrefereLeSombre()
    {
        try
        {
            using var clef = Registry.CurrentUser.OpenSubKey(PersonalisationKey);
            return clef?.GetValue(AppsUseLightThemeValue) is int valeur && valeur == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Poste verrouille par une strategie de groupe : le theme clair reste le defaut,
            // ce n'est pas une raison pour empecher l'application de demarrer.
            return false;
        }
    }

    // Une fenetre est-elle deja a l'ecran ? Sert a distinguer l'application initiale du
    // theme (rien d'affiche, donc rien a repeindre) d'un changement en cours de session.
    private static bool Affichee =>
        System.Windows.Application.Current?.Windows.Count > 0;

    // Une couleur illisible ne doit pas empecher l'application de demarrer : on retombe sur
    // la valeur claire, visible et coherente, plutot que sur du blanc arbitraire.
    private static Color Lire(string hex, Color repli)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            Debug.WriteLine($"[Theme] couleur illisible dans ThemePalette : « {hex} »");
            return repli;
        }
    }

    // Un dictionnaire et tous ses MergedDictionaries, en profondeur. Sans cela le theme -
    // charge par fusion depuis App.xaml - serait invisible : Keys ne rend que les clefs
    // propres du dictionnaire interroge.
    private static IEnumerable<ResourceDictionary> Aplatir(ResourceDictionary racine)
    {
        yield return racine;

        foreach (var fusionne in racine.MergedDictionaries)
        {
            foreach (var descendant in Aplatir(fusionne))
            {
                yield return descendant;
            }
        }
    }

    // Un brush oublie dans la palette sombre resterait clair sur fond sombre - une carte
    // blanche au milieu d'un ecran de nuit. L'oubli est trace en developpement, la ou il se
    // corrige.
    //
    // Une trace et non Debug.Assert : hors debogueur, une assertion .NET appelle FailFast et
    // l'application se fermerait au demarrage. Tuer le client pour signaler une couleur
    // manquante serait plus grave que la couleur manquante, qui se voit a l'ecran.
    [Conditional("DEBUG")]
    private static void VerifierCouverture()
    {
        var trouvees = Couleurs.Select(entree => entree.Clef).ToHashSet(StringComparer.Ordinal);

        var introuvables = ThemePalette.Sombre.Keys
            .Where(clef => !trouvees.Contains(clef))
            .OrderBy(clef => clef, StringComparer.Ordinal)
            .ToList();

        if (introuvables.Count > 0)
        {
            Debug.WriteLine("[Theme] clefs de ThemePalette absentes du dictionnaire : "
                + string.Join(", ", introuvables));
        }
    }
}
