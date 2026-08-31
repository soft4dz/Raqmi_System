using System.Globalization;
using System.Windows;
using System.Windows.Data;
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
