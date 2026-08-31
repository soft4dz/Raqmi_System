using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Trouve, dans l'ecran affiche, le controle qu'un raccourci clavier doit actionner.
///
/// Pourquoi une recherche dans l'arbre plutot qu'une interface implementee par les vues :
/// les 24 vues de module sont des UserControl autonomes, ecrits a des dates differentes,
/// et leur faire porter un contrat commun demanderait de toutes les rouvrir. Elles
/// partagent en revanche deja une convention de nommage stable, observable dans le code :
/// un bouton d'actualisation s'appelle <c>Refresh…Button</c> (50 boutons dans 23 des 24
/// vues), un enregistrement <c>Save…Button</c> (31 dans 15 vues), une creation
/// <c>New…Button</c> (17 dans 11 vues), un champ de recherche <c>…Search…TextBox</c> ou
/// <c>…Filter…TextBox</c> (11 dans 10 vues). Le routeur s'appuie sur cette convention.
///
/// Regle de proximite : la recherche part du controle qui a le focus clavier et remonte
/// ses ancetres. Une vue a onglets peut contenir plusieurs formulaires, donc plusieurs
/// boutons « Enregistrer » ; celui qu'attend l'utilisateur est celui de la section ou il
/// saisit, pas le premier de la vue. A defaut de focus, la recherche repart de la racine
/// de l'ecran.
///
/// Un raccourci sans candidat ne fait rien. Un raccourci qui devinerait serait pire que
/// pas de raccourci du tout : il declencherait un appel API que personne n'a demande.
/// </summary>
internal static class ShortcutRouter
{
    /// <summary>Bouton d'actualisation de l'ecran (F5).</summary>
    public static Button? FindRefreshButton(DependencyObject root) =>
        FindNearest<Button>(root, b => NameStartsWith(b, "Refresh"));

    /// <summary>Bouton d'enregistrement du formulaire courant (Ctrl+S).</summary>
    public static Button? FindSaveButton(DependencyObject root) =>
        FindNearest<Button>(root, b => NameStartsWith(b, "Save"));

    /// <summary>Bouton de creation (Ctrl+N).</summary>
    public static Button? FindNewButton(DependencyObject root) =>
        FindNearest<Button>(root, b => NameStartsWith(b, "New"));

    /// <summary>Champ de recherche ou de filtre de l'ecran (Ctrl+F).</summary>
    public static TextBox? FindSearchBox(DependencyObject root) =>
        FindNearest<TextBox>(root, t =>
            NameContains(t, "Search") || NameContains(t, "Filter"));

    // Remonte depuis le focus clavier : a chaque niveau, on cherche un candidat dans le
    // sous-arbre de l'ancetre, en s'arretant au premier niveau qui en contient un. On ne
    // sort jamais de <paramref name="root"/>, qui delimite l'ecran courant : un raccourci
    // ne doit pas actionner un bouton d'un autre onglet, meme charge en memoire.
    private static T? FindNearest<T>(DependencyObject root, Func<T, bool> match)
        where T : FrameworkElement
    {
        var focused = Keyboard.FocusedElement as DependencyObject;

        for (var node = focused; node is not null; node = GetParent(node))
        {
            if (!IsWithin(node, root))
            {
                // Le focus est ailleurs (barre laterale, en-tete) : la recherche de
                // proximite n'a plus de sens, on repart de la racine de l'ecran.
                break;
            }

            if (FindFirstIn(node, match) is { } near)
            {
                return near;
            }
        }

        return FindFirstIn(root, match);
    }

    // Parcours en largeur : a profondeur egale, le premier candidat est celui que
    // l'utilisateur lit en premier. Un parcours en profondeur aurait rendu le resultat
    // dependant de l'imbrication des conteneurs, invisible a l'ecran.
    private static T? FindFirstIn<T>(DependencyObject root, Func<T, bool> match)
        where T : FrameworkElement
    {
        var file = new Queue<DependencyObject>();
        file.Enqueue(root);

        while (file.Count > 0)
        {
            var node = file.Dequeue();

            if (node is T candidate && IsActionable(candidate) && match(candidate))
            {
                return candidate;
            }

            // Un sous-arbre invisible ne contient rien d'actionnable : l'onglet non
            // affiche d'une vue a onglets est ecarte ici, en un seul test.
            if (node is UIElement { Visibility: not Visibility.Visible })
            {
                continue;
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                file.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
        }

        return null;
    }

    // Actionnable = ce que l'utilisateur pourrait cliquer lui-meme a cet instant. Un
    // bouton grise par une permission manquante (regle 3.2 de la charte) le reste au
    // clavier : le raccourci ne contourne aucun droit.
    private static bool IsActionable(FrameworkElement element) =>
        element is { IsVisible: true, IsEnabled: true };

    private static bool NameStartsWith(FrameworkElement element, string prefix) =>
        element.Name.StartsWith(prefix, StringComparison.Ordinal);

    private static bool NameContains(FrameworkElement element, string fragment) =>
        element.Name.Contains(fragment, StringComparison.Ordinal);

    private static bool IsWithin(DependencyObject node, DependencyObject root)
    {
        for (var current = node; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    // Le focus clavier peut se poser dans un popup ou un ContentPresenter dont le parent
    // visuel est nul alors que le parent logique existe : les deux chaines sont suivies.
    private static DependencyObject? GetParent(DependencyObject node)
    {
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            if (VisualTreeHelper.GetParent(node) is { } visualParent)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(node);
    }
}
