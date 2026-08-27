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
}
