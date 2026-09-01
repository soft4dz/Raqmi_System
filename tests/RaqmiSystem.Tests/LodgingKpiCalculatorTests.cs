using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les formules d'hebergement, verifiees sur des cas ou le resultat se refait de tete.
/// </summary>
public sealed class LodgingKpiCalculatorTests
{
    private readonly LodgingKpiCalculator calculator = new();

    private IReadOnlyDictionary<string, KpiMeasure> Compute(KpiFactSet facts, KpiPeriod? period = null)
    {
        return calculator.Compute(period ?? January, UnitA, facts)
            .ToDictionary(measure => measure.Code);
    }

    [Fact]
    public void Occupancy_is_occupied_nights_over_available_nights()
    {
        // 2 chambres x 31 jours = 62 nuitees disponibles ; un sejour de 10 nuits = 10 occupees.
        var facts = Facts(
            rooms: Rooms(2),
            stays: [Stay(0, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 15))]);

        var measures = Compute(facts);

        Assert.Equal(62m, measures[KpiCodes.RoomsAvailable].Value);
        Assert.Equal(10m, measures[KpiCodes.RoomsOccupied].Value);
        Assert.Equal(16.13m, measures[KpiCodes.OccupancyRate].Value);
    }

    [Fact]
    public void The_departure_night_is_not_occupied()
    {
        // Convention hoteliere [arrivee, depart[ : un sejour du 5 au 6 est UNE nuit, celle du 5.
        var facts = Facts(
            rooms: Rooms(1),
            stays: [Stay(0, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6))]);

        Assert.Equal(1m, Compute(facts)[KpiCodes.RoomsOccupied].Value);
    }

    [Fact]
    public void An_out_of_order_room_leaves_the_sellable_capacity()
    {
        // 2 chambres x 31 jours, moins 10 nuits d'indisponibilite sur une chambre.
        var facts = Facts(
            rooms: Rooms(2),
            outages: [Outage(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11))]);

        var measures = Compute(facts);

        Assert.Equal(52m, measures[KpiCodes.RoomsAvailable].Value);
        Assert.Equal(10m, measures[KpiCodes.RoomsOutOfOrder].Value);
    }

    [Fact]
    public void Two_overlapping_outages_never_remove_the_same_night_twice()
    {
        var facts = Facts(
            rooms: Rooms(1),
            outages:
            [
                Outage(0, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11)),
                Outage(0, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 21))
            ]);

        var measures = Compute(facts);

        // 20 nuits couvertes au total (1 au 20), 11 restantes sur 31.
        Assert.Equal(20m, measures[KpiCodes.RoomsOutOfOrder].Value);
        Assert.Equal(11m, measures[KpiCodes.RoomsAvailable].Value);
    }

    [Fact]
    public void An_inactive_room_is_not_capacity_at_all()
    {
        var facts = Facts(
            rooms: [Room(RoomId(0)), Room(RoomId(1), isActive: false)]);

        var measures = Compute(facts);

        Assert.Equal(2m, measures[KpiCodes.PhysicalRooms].Value);
        Assert.Equal(31m, measures[KpiCodes.RoomsAvailable].Value);
    }

    [Fact]
    public void A_complimentary_night_occupies_the_room_but_is_not_sold()
    {
        var facts = Facts(
            rooms: Rooms(2),
            stays:
            [
                Stay(0, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11)),
                Stay(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), nightlyRate: 0m)
            ]);

        var measures = Compute(facts);

        Assert.Equal(15m, measures[KpiCodes.RoomsOccupied].Value);
        Assert.Equal(5m, measures[KpiCodes.ComplimentaryRooms].Value);
        Assert.Equal(10m, measures[KpiCodes.RoomsSold].Value);
    }

    [Fact]
    public void Adr_divides_room_revenue_by_sold_nights_not_by_occupied_nights()
    {
        // 10 nuits vendues, 5 offertes, 1 000 000 de recettes hebergement : ADR = 100 000.
        // Diviser par les 15 nuitees occupees donnerait 66 667 - un prix moyen que personne n'a
        // pratique.
        var facts = Facts(
            rooms: Rooms(2),
            stays:
            [
                Stay(0, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11)),
                Stay(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), nightlyRate: 0m)
            ],
            revenues: [Revenue(Jan1, accommodation: 1_000_000m)]);

        Assert.Equal(100_000m, Compute(facts)[KpiCodes.Adr].Value);
    }

    /// <summary>
    /// L'identite que reclame tout controleur de gestion : RevPAR = ADR x taux d'occupation.
    /// Elle se verifie contre le taux d'occupation VENDUE, gratuites exclues des deux cotes -
    /// c'est la seule lecture ou les deux methodes coincident exactement, et le catalogue le dit.
    /// </summary>
    [Fact]
    public void RevPar_equals_adr_times_sold_occupancy()
    {
        var facts = Facts(
            rooms: Rooms(4),
            stays:
            [
                Stay(0, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11)),
                Stay(1, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 9)),
                Stay(2, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), nightlyRate: 0m)
            ],
            revenues: [Revenue(Jan1, accommodation: 2_400_000m)]);

        var measures = Compute(facts);

        var adr = measures[KpiCodes.Adr].Value!.Value;
        var sold = measures[KpiCodes.RoomsSold].Value!.Value;
        var available = measures[KpiCodes.RoomsAvailable].Value!.Value;
        var revPar = measures[KpiCodes.RevPar].Value!.Value;

        var soldOccupancy = sold / available;

        Assert.Equal(revPar, Math.Round(adr * soldOccupancy, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void RevPar_is_null_when_there_is_no_capacity_never_zero()
    {
        // La regle centrale du volet qualite des donnees : sans chambre disponible, le RevPAR
        // n'existe pas - il ne vaut pas zero, ce qui ferait croire a un hotel sans activite.
        var facts = Facts(rooms: [], revenues: [Revenue(Jan1, accommodation: 500_000m)]);

        var revPar = Compute(facts)[KpiCodes.RevPar];

        Assert.Null(revPar.Value);
        Assert.Equal(KpiQuality.MissingData, revPar.Quality);
        Assert.NotEmpty(revPar.MissingData);
    }

    [Fact]
    public void TRevPar_counts_every_revenue_column()
    {
        var facts = Facts(
            rooms: Rooms(1),
            revenues: [Revenue(Jan1, accommodation: 100m, food: 50m, beverage: 30m, other: 20m)]);

        // 200 de CA total sur 31 nuitees disponibles.
        Assert.Equal(6.45m, Compute(facts)[KpiCodes.TRevPar].Value);
    }

    [Fact]
    public void Only_validated_revenue_feeds_the_indicators()
    {
        var facts = Facts(
            rooms: Rooms(1),
            revenues:
            [
                Revenue(Jan1, accommodation: 100m),
                Revenue(Jan1, accommodation: 900m, status: DailyRevenueStatus.Draft),
                Revenue(Jan1, accommodation: 900m, status: DailyRevenueStatus.Submitted),
                Revenue(Jan1, accommodation: 900m, status: DailyRevenueStatus.Rejected)
            ]);

        Assert.Equal(3.23m, Compute(facts)[KpiCodes.RevPar].Value);
    }

    [Fact]
    public void Alos_counts_the_whole_stay_even_when_it_overflows_the_period()
    {
        // Un sejour du 28 janvier au 4 fevrier dure 7 nuits, dont 4 seulement en janvier.
        // Amputer le sejour raccourcirait mecaniquement l'ALOS de toutes les fins de mois.
        var facts = Facts(
            rooms: Rooms(1),
            stays: [Stay(0, new DateOnly(2026, 1, 28), new DateOnly(2026, 2, 4))]);

        Assert.Equal(7m, Compute(facts)[KpiCodes.Alos].Value);
    }

    [Fact]
    public void Cancellation_rate_ignores_mere_inquiries()
    {
        // 1 annulee + 1 confirmee + 1 no-show = 3 vraies reservations ; la demande non confirmee
        // n'en est pas une et ne doit pas diluer le taux.
        var facts = Facts(
            rooms: Rooms(4),
            stays:
            [
                Stay(0, Jan1, Jan1.AddDays(2), cancelled: true, blocks: false),
                Stay(1, Jan1, Jan1.AddDays(2)),
                Stay(2, Jan1, Jan1.AddDays(2), noShow: true, blocks: false),
                Stay(3, Jan1, Jan1.AddDays(2), blocks: false)
            ]);

        var measures = Compute(facts);

        Assert.Equal(33.33m, measures[KpiCodes.CancellationRate].Value);

        // Le no-show se mesure sur les arrivees ATTENDUES : l'annulation en sort.
        Assert.Equal(50m, measures[KpiCodes.NoShowRate].Value);
    }

    [Fact]
    public void No_show_lost_revenue_is_valued_at_the_frozen_rate()
    {
        var facts = Facts(
            rooms: Rooms(1),
            stays: [Stay(0, Jan1, Jan1.AddDays(3), nightlyRate: 12_000m, noShow: true, blocks: false)]);

        Assert.Equal(36_000m, Compute(facts)[KpiCodes.NoShowLostRevenue].Value);
    }

    [Fact]
    public void Booking_lead_time_never_goes_negative()
    {
        // Un walk-in saisi apres l'arrivee compte pour zero jour, pas pour un delai negatif.
        var facts = Facts(
            rooms: Rooms(2),
            stays:
            [
                Stay(0, new DateOnly(2026, 1, 21), new DateOnly(2026, 1, 22),
                    createdOn: new DateOnly(2026, 1, 1)),
                Stay(1, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 11),
                    createdOn: new DateOnly(2026, 1, 12))
            ]);

        // (20 + 0) / 2 = 10 jours.
        Assert.Equal(10m, Compute(facts)[KpiCodes.BookingLeadTime].Value);
    }

    [Fact]
    public void Repeat_guest_rate_counts_customers_known_before_the_period()
    {
        var facts = Facts(
            rooms: Rooms(2),
            stays:
            [
                Stay(0, Jan1, Jan1.AddDays(2), customerCode: "CLI-FIDELE"),
                Stay(1, Jan1, Jan1.AddDays(2), customerCode: "CLI-NOUVEAU")
            ],
            returningCustomers: ["CLI-FIDELE"]);

        Assert.Equal(50m, Compute(facts)[KpiCodes.RepeatGuestRate].Value);
    }

    [Fact]
    public void Guest_nights_count_people_not_rooms()
    {
        var facts = Facts(
            rooms: Rooms(1),
            stays: [Stay(0, Jan1, Jan1.AddDays(4), guestCount: 3)]);

        var measures = Compute(facts);

        Assert.Equal(4m, measures[KpiCodes.RoomsOccupied].Value);
        Assert.Equal(12m, measures[KpiCodes.GuestNights].Value);
    }

    [Fact]
    public void A_hotel_without_any_activity_reports_zeros_and_dashes_never_wrong_numbers()
    {
        var measures = Compute(Facts(rooms: Rooms(3)));

        Assert.Equal(93m, measures[KpiCodes.RoomsAvailable].Value);
        Assert.Equal(0m, measures[KpiCodes.RoomsOccupied].Value);
        Assert.Equal(0m, measures[KpiCodes.OccupancyRate].Value);
        Assert.Equal(0m, measures[KpiCodes.RevPar].Value);

        // Pas une nuit vendue : le prix moyen n'a pas d'objet, il ne vaut pas zero.
        Assert.Null(measures[KpiCodes.Adr].Value);
        Assert.Equal(KpiQuality.MissingData, measures[KpiCodes.Adr].Quality);

        Assert.Null(measures[KpiCodes.Alos].Value);
        Assert.Null(measures[KpiCodes.CancellationRate].Value);
    }

    [Fact]
    public void A_period_entirely_outside_the_stays_reports_no_occupancy()
    {
        var facts = Facts(
            rooms: Rooms(1),
            stays: [Stay(0, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10))]);

        Assert.Equal(0m, Compute(facts)[KpiCodes.RoomsOccupied].Value);
    }
}
