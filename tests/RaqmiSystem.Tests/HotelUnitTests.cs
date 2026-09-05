using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Tests;

public sealed class HotelUnitTests
{
    [Fact]
    public void Constructor_normalizes_code_and_sets_defaults()
    {
        var unit = new HotelUnit(" el-manar ", "Hotel El Manar", HotelUnitType.Hotel, 10);

        Assert.Equal("EL-MANAR", unit.Code);
        Assert.Equal("Hotel El Manar", unit.Name);
        Assert.Equal(HotelUnitType.Hotel, unit.UnitType);
        Assert.Equal(10, unit.DisplayOrder);
        Assert.True(unit.IsActive);
    }

    [Fact]
    public void Deactivate_then_activate_changes_active_state()
    {
        var unit = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel);

        unit.Deactivate();
        Assert.False(unit.IsActive);

        unit.Activate();
        Assert.True(unit.IsActive);
    }

    [Fact]
    public void Constructor_rejects_negative_display_order()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HotelUnit("EL-RIADH", "Hotel El Riadh", HotelUnitType.Hotel, -1));
    }

    // --------------------------------------------------------- établissement générique

    /// <summary>
    /// Toutes les unités créées avant la notion de secteur étaient des hôtels : un appelant qui
    /// ne dit rien obtient donc l'hôtellerie, et rien d'autre ne change pour lui.
    /// </summary>
    [Fact]
    public void An_establishment_is_hospitality_unless_told_otherwise()
    {
        var legacy = new HotelUnit("HTL1", "Hotel Un", HotelUnitType.Hotel);
        var shop = new HotelUnit("BTQ1", "Boutique Centre", HotelUnitType.Shop, 3, BusinessSector.Retail);

        Assert.Equal(BusinessSector.Hospitality, legacy.Sector);
        Assert.Equal(BusinessSector.Retail, shop.Sector);
        Assert.Equal(HotelUnitType.Shop, shop.UnitType);
    }

    [Theory]
    [InlineData(HotelUnitType.Restaurant, BusinessSector.Hospitality)]
    [InlineData(HotelUnitType.Office, BusinessSector.Services)]
    [InlineData(HotelUnitType.Warehouse, BusinessSector.Manufacturing)]
    [InlineData(HotelUnitType.School, BusinessSector.Education)]
    [InlineData(HotelUnitType.Clinic, BusinessSector.Health)]
    [InlineData(HotelUnitType.Other, BusinessSector.Other)]
    public void Every_generic_type_and_sector_is_accepted(HotelUnitType unitType, BusinessSector sector)
    {
        var unit = new HotelUnit("GEN", "Etablissement", unitType, 0, sector);

        Assert.Equal(unitType, unit.UnitType);
        Assert.Equal(sector, unit.Sector);
    }

    /// <summary>
    /// Nul = inchangé : un écran ou un import qui ignore encore le secteur corrige un libellé
    /// sans remettre l'unité à l'hôtellerie. Le secteur ne bouge que quand on le demande.
    /// </summary>
    [Fact]
    public void Updating_without_a_sector_keeps_the_current_one()
    {
        var clinic = new HotelUnit("CLN1", "Clinique", HotelUnitType.Clinic, 1, BusinessSector.Health);

        clinic.UpdateDetails("Clinique du Parc", HotelUnitType.Clinic, 2);
        Assert.Equal(BusinessSector.Health, clinic.Sector);
        Assert.Equal("Clinique du Parc", clinic.Name);

        clinic.UpdateDetails("Clinique du Parc", HotelUnitType.Office, 2, BusinessSector.Services);
        Assert.Equal(BusinessSector.Services, clinic.Sector);
        Assert.Equal(HotelUnitType.Office, clinic.UnitType);
    }

    /// <summary>
    /// Une valeur hors énumération passerait la conversion en chaîne et casserait la contrainte
    /// CHECK en base ; le domaine la refuse d'abord, avec le nom de l'argument.
    /// </summary>
    [Fact]
    public void Unknown_types_and_sectors_are_rejected_before_reaching_the_database()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HotelUnit("BAD", "Type inconnu", (HotelUnitType)42));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HotelUnit("BAD", "Secteur inconnu", HotelUnitType.Hotel, 0, (BusinessSector)42));

        var unit = new HotelUnit("OK", "Valide", HotelUnitType.Hotel);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            unit.UpdateDetails("Valide", HotelUnitType.Hotel, 0, (BusinessSector)42));
    }
}
