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
