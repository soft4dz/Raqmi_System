using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace RaqmiSystem.Desktop;

// Resout l'icone vectorielle d'un groupe de modules. Les geometries sont
// declarees dans Themes/RaqmiTheme.xaml sous la cle "ModuleGroupIcon.<cle>" :
// aucun trace n'est ecrit dans la vue ni dans le code-behind.
public sealed class ModuleGroupIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string groupIconKey)
        {
            return null;
        }

        // Nom qualifie : dans l'espace de noms RaqmiSystem.*, "Application" seul
        // designerait l'espace de noms RaqmiSystem.Application.
        return System.Windows.Application.Current?.TryFindResource($"ModuleGroupIcon.{groupIconKey}") as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Nom accessible d'un controle de saisie, pour les lecteurs d'ecran.
//
// Le probleme : un TextBox, un PasswordBox, un ComboBox ou un DatePicker n'expose
// aucun nom a l'API d'automatisation. Le libelle est un TextBlock pose AU-DESSUS du
// champ - a l'ecran, l'oeil fait le lien ; pour un lecteur d'ecran, ce sont deux
// elements sans rapport, et le champ s'annonce "zone de texte, vide". Sur des ecrans
// qui alignent quinze champs, cela revient a ne rien annoncer du tout.
//
// La solution : deriver le nom de ce que la vue ecrit deja. Le Tag porte le texte
// indicatif du champ ("Nom d'utilisateur", "Rechercher un module...") - court, il dit
// exactement ce qu'on attend, c'est le meilleur candidat. A defaut, l'info-bulle, que
// ce produit renseigne largement (429 dans les vues). A defaut des deux, rien : mieux
// vaut un champ sans nom qu'un champ nomme a tort.
//
// Pose une seule fois dans le theme (styles implicites de RaqmiTheme.xaml), donc valable
// sur tous les ecrans, presents et a venir. Une vue qui pose elle-meme un
// AutomationProperties.Name garde le sien : une valeur locale prime sur un setter de style.
public sealed class AccessibleNameConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var value in values)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                // Le texte indicatif se termine souvent par des points de suspension,
                // utiles a l'ecran, parasites une fois enonces a voix haute.
                return text.TrimEnd('.', '…', ' ');
            }
        }

        return null;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Compteur d'en-tete de section : "1 module" / "13 modules".
public sealed class ModuleCountLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int count)
        {
            return null;
        }

        return count > 1 ? $"{count} modules" : $"{count} module";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Badge de maturite d'une carte de l'accueil : quatre niveaux (Planifie, Apercu technique,
// Fonctionnel, Pret pour la production), chacun avec son style de pastille.
//
// Le style est reference par cle de ressource DYNAMIQUE - "MaturityBadge.<niveau>" - et non
// choisi par un declencheur : un Style ne peut pas poser la propriete Style de l'element
// qu'il habille, WPF le refuse. SetResourceReference est exactement ce que fait
// {DynamicResource} en XAML, a ceci pres que la cle depend ici de la donnee liee.
//
// Les styles sont livres par le theme (Themes/RaqmiTheme.xaml). Tant qu'ils n'y sont pas,
// la fenetre principale enregistre sous les memes cles un repli reprenant la pastille de
// statut actuelle (MainWindow.EnsureMaturityBadgeStyles) : le badge n'est jamais nu.
public static class MaturityBadge
{
    // Nullable a dessein : la valeur par defaut d'une propriete attachee ne declenche
    // pas de rappel, et un badge « Planifie » (premiere valeur de l'enumeration) resterait
    // sans style si le defaut etait deja « Planifie ».
    public static readonly DependencyProperty MaturityProperty =
        DependencyProperty.RegisterAttached(
            "Maturity",
            typeof(RaqmiSystem.Application.Navigation.FunctionalMaturity?),
            typeof(MaturityBadge),
            new PropertyMetadata(null, OnMaturityChanged));

    public static void SetMaturity(DependencyObject element, RaqmiSystem.Application.Navigation.FunctionalMaturity? value) =>
        element.SetValue(MaturityProperty, value);

    public static RaqmiSystem.Application.Navigation.FunctionalMaturity? GetMaturity(DependencyObject element) =>
        (RaqmiSystem.Application.Navigation.FunctionalMaturity?)element.GetValue(MaturityProperty);

    /// <summary>Cle de ressource du style d'un niveau : « MaturityBadge.Functional ».</summary>
    public static string StyleKey(RaqmiSystem.Application.Navigation.FunctionalMaturity maturity) =>
        $"MaturityBadge.{maturity}";

    private static void OnMaturityChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement target)
        {
            return;
        }

        if (e.NewValue is RaqmiSystem.Application.Navigation.FunctionalMaturity maturity)
        {
            target.SetResourceReference(FrameworkElement.StyleProperty, StyleKey(maturity));
        }
        else
        {
            target.ClearValue(FrameworkElement.StyleProperty);
        }
    }
}

// Nom d'automatisation d'un en-tete de domaine de la barre laterale : « Domaine 05
// Facturation & Ventes, 1 écran » au repos, « …, 2 résultats » pendant une recherche.
//
// Toujours le nom COMPLET, jamais le libelle court : le libelle court est une contraction
// pour l'oeil, le lecteur d'ecran n'a pas de contrainte de largeur. L'etat deplie / replie
// n'est pas repete ici : le ToggleButton l'expose deja par son pattern natif.
//
// Valeurs attendues, dans l'ordre : identifiant, nom complet, nombre d'ecrans, mode recherche.
public sealed class DomainAutomationNameConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 4
            || values[0] is not string id
            || values[1] is not string name
            || values[2] is not int count
            || values[3] is not bool searching)
        {
            return null;
        }

        var unit = searching ? "résultat" : "écran";
        var plural = count > 1 ? "s" : string.Empty;

        return $"Domaine {id} {name}, {count} {unit}{plural}";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Ouvre l'info-bulle d'une rangee de la barre laterale quand elle recoit le focus CLAVIER,
// et la referme quand elle le perd.
//
// Une info-bulle WPF ne s'ouvre qu'a la souris. Or dans la barre laterale, elle porte le
// nom complet d'un libelle tronque, ou le motif d'un ecran verrouille : une information
// que l'utilisateur au clavier ne doit pas avoir a aller chercher a la souris. Le
// comportement est declare une fois dans les styles du theme (ModuleNavButton,
// ModuleNavGroup) ; il ne fait rien tant que la rangee - ou le libelle qu'elle contient -
// n'a pas d'info-bulle, ce qui est le cas de toute rangee ouvrable non tronquee.
public static class KeyboardFocusToolTip
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(KeyboardFocusToolTip),
            new PropertyMetadata(false, OnIsEnabledChanged));

    // L'info-bulle ouverte par le focus, pour la refermer au depart du focus : privee, elle
    // n'a pas a etre lue par le XAML.
    private static readonly DependencyProperty OpenedToolTipProperty =
        DependencyProperty.RegisterAttached(
            "OpenedToolTip",
            typeof(ToolTip),
            typeof(KeyboardFocusToolTip),
            new PropertyMetadata(null));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement row)
        {
            return;
        }

        row.GotKeyboardFocus -= Row_GotKeyboardFocus;
        row.LostKeyboardFocus -= Row_LostKeyboardFocus;

        if (e.NewValue is true)
        {
            row.GotKeyboardFocus += Row_GotKeyboardFocus;
            row.LostKeyboardFocus += Row_LostKeyboardFocus;
        }
    }

    private static void Row_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var row = (FrameworkElement)sender;

        // Le focus qui suit un clic vient de la souris : l'info-bulle classique s'en charge,
        // en ouvrir une seconde ferait doublon.
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice
            || FindToolTipContent(row) is not { } content)
        {
            return;
        }

        var toolTip = new ToolTip
        {
            Content = content,
            PlacementTarget = row,
            Placement = PlacementMode.Bottom,
            IsOpen = true
        };

        row.SetValue(OpenedToolTipProperty, toolTip);
    }

    private static void Row_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var row = (FrameworkElement)sender;

        if (row.GetValue(OpenedToolTipProperty) is ToolTip toolTip)
        {
            toolTip.IsOpen = false;
            row.ClearValue(OpenedToolTipProperty);
        }
    }

    // L'info-bulle de la rangee elle-meme (ecran verrouille), sinon celle du premier
    // element de son contenu qui en porte une (libelle tronque : voir TrimmedTextToolTip).
    private static object? FindToolTipContent(DependencyObject root)
    {
        if (root is FrameworkElement { ToolTip: { } own })
        {
            return own is ToolTip control ? control.Content : own;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindToolTipContent(VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

// Info-bulle d'un libelle de la barre laterale, posee SEULEMENT quand le texte a ete
// tronque : le nom complet d'un domaine dont le libelle court deborde, l'intitule d'un
// ecran coupe apres deux lignes. Une rangee dont le texte tient entier n'a pas d'info-bulle
// (sobriete : pas de bulle de description au survol de chaque ligne).
//
// WPF (.NET) n'expose pas TextBlock.IsTextTrimmed : la troncature est donc recalculee ici a
// chaque changement de taille, en mesurant le texte avec la police du TextBlock dans la
// largeur qu'il a recue - sur une ligne s'il ne replie pas, sinon en comparant la hauteur
// necessaire a la hauteur qui lui est permise (MaxHeight = deux interlignes).
public static class TrimmedTextToolTip
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(TrimmedTextToolTip),
            new PropertyMetadata(null, OnTextChanged));

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        textBlock.SizeChanged -= TextBlock_SizeChanged;

        if (e.NewValue is string)
        {
            textBlock.SizeChanged += TextBlock_SizeChanged;
        }

        Refresh(textBlock);
    }

    private static void TextBlock_SizeChanged(object sender, SizeChangedEventArgs e) => Refresh((TextBlock)sender);

    private static void Refresh(TextBlock textBlock)
    {
        textBlock.ToolTip = GetText(textBlock) is { } content && IsTrimmed(textBlock) ? content : null;
    }

    private static bool IsTrimmed(TextBlock textBlock)
    {
        if (textBlock.ActualWidth <= 0 || string.IsNullOrEmpty(textBlock.Text))
        {
            return false;
        }

        var formatted = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            textBlock.Foreground,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);

        var available = textBlock.ActualWidth - textBlock.Padding.Left - textBlock.Padding.Right;

        if (textBlock.TextWrapping == TextWrapping.NoWrap)
        {
            return formatted.Width > available + 0.5;
        }

        formatted.MaxTextWidth = Math.Max(1, available);

        if (textBlock.LineStackingStrategy == LineStackingStrategy.BlockLineHeight && textBlock.LineHeight > 0)
        {
            formatted.LineHeight = textBlock.LineHeight;
        }

        var allowedHeight = double.IsPositiveInfinity(textBlock.MaxHeight) ? textBlock.ActualHeight : textBlock.MaxHeight;

        return formatted.Height > allowedHeight + 0.5;
    }
}
