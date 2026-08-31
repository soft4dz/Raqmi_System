using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Relie un champ de saisie au libelle qui le precede, pour les lecteurs d'ecran.
///
/// Le probleme : dans ce produit, un champ s'ecrit partout de la meme facon - un
/// <c>TextBlock</c> de style <c>LabelText</c>, puis le champ, dans le meme conteneur
/// (222 champs sur 412 suivent cette forme). L'oeil fait le lien par la mise en page ;
/// l'API d'automatisation, elle, voit deux elements sans rapport, et annonce « zone de
/// texte, vide » sur un ecran qui en aligne quinze.
///
/// La reponse : poser <see cref="AutomationProperties.LabeledByProperty"/> - la relation
/// explicite entre un libelle et son champ, plus juste qu'un nom recopie, car elle survit
/// a un changement de libelle et se lit comme telle par les outils d'accessibilite.
///
/// Le lien est etabli au chargement du champ, uniquement si :
///   - le champ n'a pas deja un nom accessible (le texte indicatif ou l'info-bulle en
///     donnent un a 228 champs, via AccessibleNameConverter) ;
///   - l'element qui le precede immediatement dans le meme conteneur est un TextBlock de
///     style <c>LabelText</c>.
/// La deuxieme condition est stricte a dessein : dans une <c>Grid</c>, l'ordre de
/// declaration ne dit rien de l'ordre a l'ecran, et rapprocher deux elements au hasard
/// ferait annoncer un libelle faux - pire que pas de libelle du tout.
/// </summary>
public static class AutomationLabels
{
    /// <summary>
    /// Pose a <c>True</c> par les styles implicites des controles de saisie
    /// (<c>Themes/RaqmiTheme.xaml</c>) : tous les ecrans en beneficient, sans qu'aucune
    /// vue ait a le declarer.
    /// </summary>
    public static readonly DependencyProperty LinkPrecedingLabelProperty =
        DependencyProperty.RegisterAttached(
            "LinkPrecedingLabel",
            typeof(bool),
            typeof(AutomationLabels),
            new PropertyMetadata(false, OnLinkPrecedingLabelChanged));

    public static void SetLinkPrecedingLabel(DependencyObject element, bool value) =>
        element.SetValue(LinkPrecedingLabelProperty, value);

    public static bool GetLinkPrecedingLabel(DependencyObject element) =>
        (bool)element.GetValue(LinkPrecedingLabelProperty);

    private static void OnLinkPrecedingLabelChanged(
        DependencyObject element,
        DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement field)
        {
            return;
        }

        if (e.NewValue is not true)
        {
            field.Loaded -= OnFieldLoaded;
            return;
        }

        // Le parent n'existe pas encore quand le style s'applique : le lien attend le
        // chargement. Desabonnement systematique avant abonnement - un champ place dans un
        // onglet peut etre charge plusieurs fois.
        field.Loaded -= OnFieldLoaded;
        field.Loaded += OnFieldLoaded;
    }

    private static void OnFieldLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement field)
        {
            return;
        }

        // Deja nomme (texte indicatif ou info-bulle) ou deja relie : ne rien defaire.
        if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(field))
            || AutomationProperties.GetLabeledBy(field) is not null)
        {
            return;
        }

        if (FindPrecedingLabel(field) is { } label)
        {
            AutomationProperties.SetLabeledBy(field, label);
        }
    }

    private static TextBlock? FindPrecedingLabel(FrameworkElement field)
    {
        if (field.Parent is not Panel panel)
        {
            return null;
        }

        var index = panel.Children.IndexOf(field);
        if (index <= 0)
        {
            return null;
        }

        return panel.Children[index - 1] is TextBlock previous && IsFieldLabel(previous)
            ? previous
            : null;
    }

    // Le style LabelText est la marque d'un libelle de champ dans ce produit (charte,
    // § 1.3). S'y limiter evite de prendre pour un libelle le titre de section ou la
    // legende qui precederaient un champ pour une tout autre raison.
    private static bool IsFieldLabel(TextBlock candidate)
    {
        if (candidate.Style is null || string.IsNullOrWhiteSpace(candidate.Text))
        {
            return false;
        }

        return ReferenceEquals(
            candidate.Style,
            System.Windows.Application.Current?.TryFindResource("LabelText"));
    }
}
