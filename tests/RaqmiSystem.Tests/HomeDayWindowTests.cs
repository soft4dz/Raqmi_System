using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Tests;

/// <summary>
/// Bornes de la journée du poste pour les sources qui prennent un instant (relevés HACCP).
/// </summary>
/// <remarks>
/// Régression : la forme naïve <c>new DateTimeOffset(DateTime.Today, TimeSpan.Zero)</c> lève
/// <c>ArgumentException</c> sur tout poste qui n'est pas à UTC — l'Algérie est à UTC+1. Cette
/// exception-là n'est pas une erreur d'appel : elle ne passe pas par le try/catch du contrat de
/// vue, elle remonte au Dispatcher et ferme l'application au milieu du chargement de l'accueil.
/// </remarks>
public sealed class HomeDayWindowTests
{
    private static readonly TimeZoneInfo WestCentralAfrica =
        TimeZoneInfo.CreateCustomTimeZone("Test/UTC+1", TimeSpan.FromHours(1), "Test UTC+1", "Test UTC+1");

    private static readonly TimeZoneInfo Marquesas =
        TimeZoneInfo.CreateCustomTimeZone("Test/UTC-9:30", TimeSpan.FromMinutes(-570), "Test UTC-9:30", "Test UTC-9:30");

    [Fact]
    public void The_day_is_bounded_with_the_offset_of_the_station_not_with_utc()
    {
        var start = HomeDayWindow.Start(new DateTime(2026, 9, 1), WestCentralAfrica);
        var end = HomeDayWindow.End(new DateTime(2026, 9, 1), WestCentralAfrica);

        Assert.Equal(TimeSpan.FromHours(1), start.Offset);
        Assert.Equal(new DateTime(2026, 9, 1), start.DateTime);
        Assert.Equal(new DateTime(2026, 9, 2), end.DateTime);
        Assert.Equal(TimeSpan.FromHours(24), end - start);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(-570)]
    [InlineData(840)]
    public void No_offset_makes_the_construction_throw(int offsetMinutes)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone(
            $"Test/{offsetMinutes}",
            TimeSpan.FromMinutes(offsetMinutes),
            "Test",
            "Test");

        var start = HomeDayWindow.Start(DateTime.Today, zone);

        Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), start.Offset);
    }

    [Fact]
    public void The_station_timezone_of_this_machine_is_accepted()
    {
        // Sans fuseau explicite : exactement ce que la vue appelle, sur le fuseau du poste.
        var start = HomeDayWindow.Start(DateTime.Today);
        var end = HomeDayWindow.End(DateTime.Today);

        Assert.True(end > start);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(DateTime.Today), start.Offset);
    }

    [Fact]
    public void A_time_component_never_leaks_into_the_boundary()
    {
        var start = HomeDayWindow.Start(new DateTime(2026, 9, 1, 17, 42, 13), Marquesas);

        Assert.Equal(new DateTime(2026, 9, 1), start.DateTime);
    }
}
