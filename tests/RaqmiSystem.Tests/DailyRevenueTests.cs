using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

public sealed class DailyRevenueTests
{
    private static readonly DateOnly BusinessDate = new(2026, 1, 31);

    [Fact]
    public void Constructor_calculates_total_and_normalizes_unit_code()
    {
        var revenue = new DailyRevenue(
            new DateOnly(2026, 1, 31),
            " el-manar ",
            1_200_000m,
            340_000m,
            110_000m,
            80_000m,
            " Journee normale ");

        Assert.Equal("EL-MANAR", revenue.HotelUnitCode);
        Assert.Equal(1_730_000m, revenue.Total);
        Assert.Equal("Journee normale", revenue.Notes);
        Assert.Equal(DailyRevenueStatus.Draft, revenue.Status);
        Assert.True(revenue.CanEdit);
    }

    [Fact]
    public void Workflow_submits_then_validates_entry()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);
        revenue.Validate("director", DateTimeOffset.UtcNow);

        Assert.Equal(DailyRevenueStatus.Validated, revenue.Status);
        Assert.False(revenue.CanEdit);
        Assert.Equal("director", revenue.ValidatedBy);
    }

    [Fact]
    public void Submitted_entry_cannot_be_edited()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            revenue.UpdateAmounts(100m, 0m, 0m, 0m, null));

        Assert.Throws<InvalidOperationException>(() =>
            revenue.UpdateLines([new DailyRevenueLine(RevenueCategoryCodes.Food, 1m)], null));
    }

    [Fact]
    public void Rejected_entry_can_be_corrected_and_returns_to_draft()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);
        revenue.Reject("Missing control sheet.", "director", DateTimeOffset.UtcNow);
        revenue.UpdateAmounts(100m, 20m, 10m, 5m, "Corrected");

        Assert.Equal(DailyRevenueStatus.Draft, revenue.Status);
        Assert.Equal(135m, revenue.Total);
        Assert.Equal("Corrected", revenue.Notes);
        Assert.Null(revenue.RejectionReason);
        Assert.True(revenue.CanEdit);
    }

    [Fact]
    public void Constructor_rejects_negative_amounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyRevenue(new DateOnly(2026, 1, 31), "EL-MANAR", -1m, 0m, 0m, 0m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyRevenue(BusinessDate, "EL-MANAR", [new DailyRevenueLine(RevenueCategoryCodes.Merchandise, -1m)]));
    }

    /// <summary>
    /// L'ancien constructeur à quatre montants et la forme par lignes décrivent la même recette :
    /// mêmes lignes, mêmes accesseurs, même total. C'est ce qui permet d'accepter l'ancien corps
    /// de requête sans qu'il existe deux modèles.
    /// </summary>
    [Fact]
    public void Legacy_four_amount_constructor_maps_to_the_hotel_category_lines()
    {
        var legacy = new DailyRevenue(BusinessDate, "EL-MANAR", 1_000m, 200m, 50m, 10m);

        var byLines = new DailyRevenue(
            BusinessDate,
            "EL-MANAR",
            [
                new DailyRevenueLine(RevenueCategoryCodes.Accommodation, 1_000m),
                new DailyRevenueLine(RevenueCategoryCodes.Food, 200m),
                new DailyRevenueLine(RevenueCategoryCodes.Beverage, 50m),
                new DailyRevenueLine(RevenueCategoryCodes.Other, 10m)
            ]);

        Assert.Equal(Cells(legacy), Cells(byLines));
        Assert.Equal(
            [RevenueCategoryCodes.Accommodation, RevenueCategoryCodes.Food, RevenueCategoryCodes.Beverage, RevenueCategoryCodes.Other],
            legacy.Lines.Select(line => line.CategoryCode).OrderBy(code => Array.IndexOf(HotelOrder, code)));

        foreach (var revenue in new[] { legacy, byLines })
        {
            Assert.Equal(1_000m, revenue.Accommodation);
            Assert.Equal(200m, revenue.Food);
            Assert.Equal(50m, revenue.Beverage);
            Assert.Equal(10m, revenue.Other);
            Assert.Equal(1_260m, revenue.Total);
            Assert.Equal(1_000m, revenue.AmountOf("accommodation"));
        }
    }

    /// <summary>
    /// Les quatre accesseurs de compatibilité sont dérivés des lignes : les modules qui les lisent
    /// (KPI, pilotage, états) obtiennent zéro pour une catégorie absente et retrouvent TOUT le
    /// reste dans Other, si bien que Accommodation + Food + Beverage + Other == Total quelle que
    /// soit la liste des catégories de l'entreprise.
    /// </summary>
    [Fact]
    public void Legacy_accessors_are_derived_from_lines_and_always_add_up_to_the_total()
    {
        var shop = new DailyRevenue(
            BusinessDate,
            "SHOP",
            [
                new DailyRevenueLine(RevenueCategoryCodes.Merchandise, 800m),
                new DailyRevenueLine(RevenueCategoryCodes.Services, 150m)
            ]);

        Assert.Equal(0m, shop.Accommodation);
        Assert.Equal(0m, shop.Food);
        Assert.Equal(0m, shop.Beverage);
        Assert.Equal(950m, shop.Other);
        Assert.Equal(950m, shop.Total);

        var hotelWithSpa = new DailyRevenue(
            BusinessDate,
            "EL-MANAR",
            [
                new DailyRevenueLine(RevenueCategoryCodes.Accommodation, 1_000m),
                new DailyRevenueLine(RevenueCategoryCodes.Food, 200m),
                new DailyRevenueLine(RevenueCategoryCodes.Beverage, 50m),
                new DailyRevenueLine(RevenueCategoryCodes.Other, 10m),
                new DailyRevenueLine("SPA", 40m)
            ]);

        Assert.Equal(1_000m, hotelWithSpa.Accommodation);
        Assert.Equal(200m, hotelWithSpa.Food);
        Assert.Equal(50m, hotelWithSpa.Beverage);
        Assert.Equal(50m, hotelWithSpa.Other);
        Assert.Equal(1_300m, hotelWithSpa.Total);

        foreach (var revenue in new[] { shop, hotelWithSpa })
        {
            Assert.Equal(revenue.Total, revenue.Accommodation + revenue.Food + revenue.Beverage + revenue.Other);
            Assert.Equal(revenue.Total, revenue.Lines.Sum(line => line.Amount));
        }
    }

    [Fact]
    public void Zero_amounts_produce_no_line_and_updating_lines_reconciles_in_place()
    {
        var revenue = new DailyRevenue(BusinessDate, "EL-MANAR", 1_000m, 0m, 0m, 0m);

        var kept = Assert.Single(revenue.Lines);
        Assert.Equal(RevenueCategoryCodes.Accommodation, kept.CategoryCode);

        revenue.UpdateLines(
            [
                new DailyRevenueLine(RevenueCategoryCodes.Accommodation, 1_200m),
                new DailyRevenueLine(RevenueCategoryCodes.Food, 300m)
            ],
            "Ajustement");

        Assert.Equal(2, revenue.Lines.Count);

        // La ligne survivante est ajustée sur place, Id compris : supprimer puis réinsérer
        // ferait collisionner l'index unique (daily_revenue_id, category_code) dans SaveChanges.
        var accommodation = revenue.Lines.Single(line => line.CategoryCode == RevenueCategoryCodes.Accommodation);
        Assert.Equal(kept.Id, accommodation.Id);
        Assert.Equal(1_200m, accommodation.Amount);
        Assert.Equal(1_200m, revenue.Accommodation);
        Assert.Equal(300m, revenue.Food);
        Assert.Equal(1_500m, revenue.Total);
        Assert.Equal("Ajustement", revenue.Notes);

        // Un montant à zéro retire la ligne ; une catégorie absente aussi.
        revenue.UpdateLines([new DailyRevenueLine(RevenueCategoryCodes.Food, 0m)], null);

        Assert.Empty(revenue.Lines);
        Assert.Equal(0m, revenue.Total);
    }

    [Fact]
    public void Duplicate_category_and_malformed_code_are_refused()
    {
        Assert.Throws<ArgumentException>(() => new DailyRevenue(
            BusinessDate,
            "EL-MANAR",
            [
                new DailyRevenueLine(RevenueCategoryCodes.Accommodation, 1m),
                new DailyRevenueLine(RevenueCategoryCodes.Accommodation, 2m)
            ]));

        Assert.Throws<ArgumentException>(() => new DailyRevenueLine("bad code!", 1m));
        Assert.Throws<ArgumentException>(() => new DailyRevenueLine(" ", 1m));

        Assert.Equal("SPA", new DailyRevenueLine(" spa ", 1m).CategoryCode);
    }

    private static readonly string[] HotelOrder =
    [
        RevenueCategoryCodes.Accommodation,
        RevenueCategoryCodes.Food,
        RevenueCategoryCodes.Beverage,
        RevenueCategoryCodes.Other
    ];

    private static IReadOnlyList<(string Code, decimal Amount)> Cells(DailyRevenue revenue)
    {
        return revenue.Lines
            .Select(line => (line.CategoryCode, line.Amount))
            .OrderBy(cell => cell.CategoryCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static DailyRevenue CreateDraft()
    {
        return new DailyRevenue(new DateOnly(2026, 1, 31), "EL-MANAR", 100m, 20m, 10m, 5m);
    }
}
