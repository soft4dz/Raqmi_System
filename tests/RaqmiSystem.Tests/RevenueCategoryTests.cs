using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le catalogue des catégories de recettes et sa règle de sélection par secteur : un hôtel doit
/// retrouver exactement ses quatre anciennes colonnes, une entreprise d'un autre secteur le jeu
/// générique, sans activation manuelle dans un cas comme dans l'autre.
/// </summary>
public sealed class RevenueCategoryTests
{
    [Fact]
    public void Hospitality_defaults_are_the_four_historical_columns_in_order()
    {
        var hotel = RevenueCategoryCatalog.DefaultsFor(BusinessSector.Hospitality);

        Assert.Equal(
            [RevenueCategoryCodes.Accommodation, RevenueCategoryCodes.Food, RevenueCategoryCodes.Beverage, RevenueCategoryCodes.Other],
            hotel.Select(category => category.Code));

        Assert.All(hotel, category => Assert.Equal(BusinessSector.Hospitality, category.Sector));
        Assert.All(hotel, category => Assert.True(category.IsActive));
        Assert.Equal(["Hébergement", "Restauration", "Bar", "Autres"], hotel.Select(category => category.Label));
    }

    [Theory]
    [InlineData(BusinessSector.Retail)]
    [InlineData(BusinessSector.Services)]
    [InlineData(BusinessSector.Manufacturing)]
    [InlineData(BusinessSector.Other)]
    [InlineData(null)]
    public void Sectors_without_a_dedicated_set_get_the_generic_trio(BusinessSector? sector)
    {
        var defaults = RevenueCategoryCatalog.DefaultsFor(sector);

        Assert.Equal(
            [RevenueCategoryCodes.Merchandise, RevenueCategoryCodes.Services, RevenueCategoryCodes.OtherIncome],
            defaults.Select(category => category.Code));

        Assert.All(defaults, category => Assert.Null(category.Sector));
    }

    [Fact]
    public void Applicable_prefers_the_dedicated_set_and_only_falls_back_to_generic_without_one()
    {
        var configured = RevenueCategoryCatalog.All
            .Append(new RevenueCategory("SPA", "Spa et bien-être", 35, BusinessSector.Hospitality))
            .Append(new RevenueCategory("DELIVERY", "Livraisons", 125))
            .ToArray();

        // L'hôtel voit son jeu dédié, enrichi de la catégorie qui le cible, à sa place dans
        // l'ordre d'affichage - et rien du jeu générique.
        Assert.Equal(
            [RevenueCategoryCodes.Accommodation, RevenueCategoryCodes.Food, RevenueCategoryCodes.Beverage, "SPA", RevenueCategoryCodes.Other],
            RevenueCategoryCatalog.Applicable(configured, BusinessSector.Hospitality).Select(category => category.Code));

        // Un secteur sans jeu dédié voit le jeu générique, enrichi de la catégorie sans secteur.
        Assert.Equal(
            [RevenueCategoryCodes.Merchandise, RevenueCategoryCodes.Services, "DELIVERY", RevenueCategoryCodes.OtherIncome],
            RevenueCategoryCatalog.Applicable(configured, BusinessSector.Retail).Select(category => category.Code));

        // Une catégorie dédiée à un autre secteur crée un jeu pour ce secteur : le générique s'efface.
        var withIndustry = configured.Append(new RevenueCategory("PRODUCTION", "Production vendue", 5, BusinessSector.Manufacturing));

        Assert.Equal(
            ["PRODUCTION"],
            RevenueCategoryCatalog.Applicable(withIndustry, BusinessSector.Manufacturing).Select(category => category.Code));
    }

    [Fact]
    public void Catalog_codes_are_unique_well_formed_and_never_collide_across_sets()
    {
        var codes = RevenueCategoryCatalog.All.Select(category => category.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Equal(code, RevenueCategoryCodes.Normalize(code)));
        Assert.Equal(RevenueCategoryCatalog.Hotel.Count + RevenueCategoryCatalog.Generic.Count, RevenueCategoryCatalog.All.Count);
    }

    [Fact]
    public void Category_normalizes_its_code_and_validates_label_order_and_sector()
    {
        var spa = new RevenueCategory(" spa ", "  Spa et bien-être  ", 35, BusinessSector.Hospitality);

        Assert.Equal("SPA", spa.Code);
        Assert.Equal("Spa et bien-être", spa.Label);
        Assert.Equal(35, spa.DisplayOrder);
        Assert.True(spa.IsActive);

        spa.Update("Spa", 36, null, isActive: false);

        Assert.Equal("Spa", spa.Label);
        Assert.Equal(36, spa.DisplayOrder);
        Assert.Null(spa.Sector);
        Assert.False(spa.IsActive);

        Assert.Throws<ArgumentException>(() => new RevenueCategory("bad code!", "Libellé", 1));
        Assert.Throws<ArgumentException>(() => new RevenueCategory("A", "Libellé", 1));
        Assert.Throws<ArgumentException>(() => new RevenueCategory("OK_CODE", " ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RevenueCategory("OK_CODE", "Libellé", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RevenueCategory("OK_CODE", "Libellé", 1, (BusinessSector)42));
    }

    [Fact]
    public void Every_known_unit_type_belongs_to_hospitality_today()
    {
        Assert.All(
            Enum.GetValues<HotelUnitType>(),
            unitType => Assert.Equal(BusinessSector.Hospitality, RevenueCategoryCatalog.SectorOf(unitType)));
    }
}
