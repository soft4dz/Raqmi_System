using System.Globalization;
using System.Windows.Data;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

/// <summary>
/// En-tete d'un domaine dans le catalogue des modules : ce que
/// <c>ModuleCatalogGroupHeaderTemplate</c> affiche.
/// </summary>
/// <remarks>
/// Un <c>record</c> et non une chaine : l'en-tete porte cinq informations (numero, nom,
/// icone, maturite, libelle de maturite) et l'egalite par valeur est ce qui permet a
/// <see cref="System.Windows.Data.PropertyGroupDescription"/> de regrouper les tuiles.
///
/// C'est le seul endroit du catalogue ou le badge de maturite s'affiche : la maturite est
/// une propriete du DOMAINE, pas de la carte (navigation-shell 6.3). La carte garde sa
/// pastille de statut, qui dit autre chose - ou en est ce module-la.
/// </remarks>
public sealed record HomeCatalogDomainHeader(
    string Id,
    string Label,
    string IconKey,
    FunctionalMaturity Maturity,
    string MaturityLabel)
{
    /// <summary>« 06 PMS / Hébergement » : le numero de domaine est visible sur l'accueil.</summary>
    public string Title => $"{Id} {Label}";

    public static HomeCatalogDomainHeader From(FunctionalDomainDefinition domain) =>
        new(domain.Id, domain.Name, domain.IconKey, domain.Maturity, FunctionalMaturityMapper.Label(domain.Maturity));
}

/// <summary>
/// Identifiant de domaine d'une tuile -> en-tete de son groupe. Passe a
/// <c>PropertyGroupDescription</c> : le catalogue est regroupe par domaine (22 en-tetes)
/// et non plus par couple domaine-module (une trentaine), sans qu'aucune propriete ne soit
/// ajoutee au modele partage avec la barre laterale.
/// </summary>
public sealed class HomeCatalogDomainConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, HomeCatalogDomainHeader> ByDomainId =
        FunctionalArchitectureCatalog.Domains.ToDictionary(
            domain => domain.Id,
            HomeCatalogDomainHeader.From,
            StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string id && ByDomainId.TryGetValue(id, out var header) ? header : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Le regroupement du catalogue est a sens unique.");
}
