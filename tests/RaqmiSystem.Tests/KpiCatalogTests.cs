using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Tests;

/// <summary>
/// La coherence du catalogue. Il est la source unique de verite de toute la bibliotheque : une
/// fiche incomplete ou incoherente se propagerait a l'API, aux ecrans et a l'historique, aussi
/// est-elle verifiee ici plutot que decouverte en production.
/// </summary>
public sealed class KpiCatalogTests
{
    [Fact]
    public void Codes_are_unique()
    {
        var codes = KpiCatalog.All.Select(definition => definition.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Codes_follow_the_upper_snake_case_convention()
    {
        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.Equal(definition.Code, definition.Code.ToUpperInvariant());
            Assert.DoesNotContain(" ", definition.Code, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Every_definition_carries_the_fields_a_reader_needs()
    {
        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Name), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.ShortName), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.Description), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.Formula), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.SourceDetail), definition.Code);
            Assert.True(definition.FormulaVersion >= 1, definition.Code);
        });
    }

    [Fact]
    public void Every_definition_requires_at_least_one_known_permission()
    {
        var known = PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.NotEmpty(definition.RequiredPermissions);
            Assert.All(definition.RequiredPermissions, permission =>
                Assert.True(known.Contains(permission), $"{definition.Code} exige la cle inconnue {permission}."));
        });
    }

    [Fact]
    public void An_indicator_awaiting_its_source_names_what_is_missing()
    {
        // C'est tout l'interet de les declarer : dire ce qu'il faudrait construire, precisement.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Availability == KpiAvailability.AwaitingSource),
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.MissingSource), definition.Code);
                Assert.Equal(KpiSourceModule.None, definition.SourceModule);
            });
    }

    [Fact]
    public void An_implemented_indicator_names_the_module_that_owns_its_data()
    {
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Availability == KpiAvailability.Implemented),
            definition =>
            {
                Assert.NotEqual(KpiSourceModule.None, definition.SourceModule);
                Assert.Null(definition.MissingSource);
            });
    }

    [Fact]
    public void A_neutral_indicator_never_declares_a_polarity_dependent_reading()
    {
        // Une capacite ou un effectif ne se juge pas : le moteur ne rend donc jamais de verdict
        // dessus, et c'est ce que la polarite neutre signifie.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Polarity == KpiPolarity.Neutral),
            definition => Assert.Equal(
                KpiHealth.Unknown,
                KpiMath.Classify(1m, 10m, 0m, definition.Polarity)));
    }

    [Fact]
    public void Headline_and_benchmark_codes_all_exist_in_the_catalog()
    {
        Assert.All(KpiCatalog.DirectionHeadlineCodes, code => Assert.NotNull(KpiCatalog.Find(code)));
        Assert.All(KpiCatalog.BenchmarkCodes, code => Assert.NotNull(KpiCatalog.Find(code)));
    }

    /// <summary>
    /// Les colonnes du comparatif sont soit des TAUX ET RATIOS, comparables entre hotels de
    /// tailles differentes, soit deux grandeurs de CONTEXTE explicitement admises - le chiffre
    /// d'affaires et l'excedent brut d'exploitation, que la direction veut voir en regard des
    /// taux. Une troisieme grandeur additive glissee dans ce tableau reviendrait a classer les
    /// hotels par nombre de chambres, ce que personne n'a besoin d'un tableau de bord pour
    /// savoir : le test la refuse.
    /// </summary>
    [Fact]
    public void Benchmark_columns_are_ratios_or_one_of_the_two_admitted_context_volumes()
    {
        string[] contextVolumes = [KpiCodes.RevenueTotal, KpiCodes.Ebitda];

        Assert.All(
            KpiCatalog.BenchmarkCodes.Where(code => !contextVolumes.Contains(code)),
            code =>
            {
                var definition = KpiCatalog.Require(code);

                Assert.Equal(KpiAggregation.RatioOfSums, definition.Aggregation);
                Assert.True(
                    definition.Unit is KpiUnit.Percentage or KpiUnit.Currency or KpiUnit.Ratio,
                    $"{code} n'est pas une colonne de comparatif exploitable.");
            });
    }

    /// <summary>
    /// Consequence a connaitre du comparatif : la colonne EBE reste VIDE unite par unite, et
    /// n'est renseignee que sur la ligne du groupe. La comptabilite de Raqmi System n'est pas
    /// analytique - une ecriture ne porte pas d'unite hoteliere - et repartir le resultat au
    /// prorata d'une cle quelconque fabriquerait un chiffre convaincant et sans fondement. Le
    /// jour ou une comptabilite analytique existe, c'est cette declaration qui change.
    /// </summary>
    [Fact]
    public void The_operating_result_column_of_the_benchmark_is_group_only()
    {
        Assert.Equal(KpiScopeLevel.GroupOnly, KpiCatalog.Require(KpiCodes.Ebitda).ScopeLevel);
        Assert.Equal(KpiScopeLevel.UnitAndGroup, KpiCatalog.Require(KpiCodes.RevenueTotal).ScopeLevel);
    }

    [Fact]
    public void A_group_only_indicator_reads_data_that_carries_no_hotel_unit()
    {
        // Les seuls modules concernes sont la comptabilite (pas d'analytique) et les ordres de
        // paiement (pas d'unite). Ajouter un autre module ici serait un choix, pas un constat.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.ScopeLevel == KpiScopeLevel.GroupOnly),
            definition => Assert.True(
                definition.SourceModule is KpiSourceModule.Accounting or KpiSourceModule.Treasury
                    or KpiSourceModule.None,
                $"{definition.Code} est declare groupe seulement sans que sa source le justifie."));
    }

    [Fact]
    public void Require_rejects_an_unknown_code()
    {
        Assert.Throws<ArgumentException>(() => KpiCatalog.Require("PAS_UN_INDICATEUR"));
        Assert.Null(KpiCatalog.Find("PAS_UN_INDICATEUR"));
        Assert.Null(KpiCatalog.Find(null));
    }

    [Fact]
    public void The_library_covers_every_requested_family()
    {
        Assert.All(
            Enum.GetValues<KpiCategory>(),
            category => Assert.NotEmpty(KpiCatalog.InCategory(category)));
    }

    // ------------------------------------------------------------ socle / pack hotelier

    /// <summary>
    /// Scinder le catalogue en socle et pack hotelier n'a le droit de rien changer a ce que les
    /// ecrans montrent : memes 86 fiches, memes codes, meme ordre. Cette liste EST l'ordre
    /// historique ; une fiche ajoutee ou deplacee se declare ici, en connaissance de cause.
    /// </summary>
    [Fact]
    public void The_split_kept_the_86_definitions_in_their_historical_order()
    {
        string[] historicalOrder =
        [
            // Hebergement
            KpiCodes.OccupancyRate, KpiCodes.PhysicalRooms, KpiCodes.RoomsAvailable, KpiCodes.RoomsOutOfOrder,
            KpiCodes.RoomsOccupied, KpiCodes.ComplimentaryRooms, KpiCodes.RoomsSold, KpiCodes.Adr, KpiCodes.RevPar,
            KpiCodes.TRevPar, KpiCodes.GopPar, KpiCodes.Alos, KpiCodes.CancellationRate, KpiCodes.NoShowRate,
            KpiCodes.NoShowLostRevenue, KpiCodes.GuestNights, KpiCodes.RevenuePerGuest, KpiCodes.BookingLeadTime,
            KpiCodes.Cpor,
            // Finance
            KpiCodes.RevenueTotal, KpiCodes.RevenueAccommodation, KpiCodes.RevenueFood, KpiCodes.RevenueBeverage,
            KpiCodes.RevenueOther, KpiCodes.RevenueBudgetVariance, KpiCodes.RevenueBudgetAchievement,
            KpiCodes.GrossOperatingProfit, KpiCodes.Ebitda, KpiCodes.GrossMarginRate, KpiCodes.OperatingMarginRate,
            KpiCodes.CashIn, KpiCodes.CashOut, KpiCodes.OperatingCashFlow, KpiCodes.CashBalance,
            KpiCodes.CommittedOutflow7D, KpiCodes.CommittedOutflow30D, KpiCodes.CommittedOutflow90D, KpiCodes.Dso,
            KpiCodes.ReceivablesTotal, KpiCodes.ReceivablesOver90, KpiCodes.ReceivablesOverdueRate,
            // Restauration et boissons
            KpiCodes.FoodCostAmount, KpiCodes.FoodCostRate, KpiCodes.BeverageCostAmount, KpiCodes.BeverageCostRate,
            KpiCodes.TotalCostOfSalesRate, KpiCodes.TheoreticalFoodCostRate, KpiCodes.FoodCostVariance,
            KpiCodes.AverageCheck, KpiCodes.RevPash, KpiCodes.CostPerCover, KpiCodes.WasteCost, KpiCodes.WasteRate,
            // Ressources humaines
            KpiCodes.PayrollCost, KpiCodes.PayrollToRevenueRate, KpiCodes.PayrollCostPerEmployee,
            KpiCodes.PayrollCostPerAvailableRoom, KpiCodes.PayrollCostPerOccupiedRoom, KpiCodes.AbsenteeismRate,
            KpiCodes.TurnoverRate, KpiCodes.HeadcountAverage, KpiCodes.OvertimeRate, KpiCodes.RevenuePerEmployee,
            KpiCodes.RevenuePerWorkedHour, KpiCodes.RoomsCleanedPerAttendant, KpiCodes.CoversPerWaiter,
            KpiCodes.InterventionsPerTechnician,
            // Maintenance
            KpiCodes.Mttr, KpiCodes.Mtbf, KpiCodes.PreventiveCompletionRate, KpiCodes.MaintenanceCostPerEquipment,
            KpiCodes.MaintenanceCostToAssetValue,
            // Experience client
            KpiCodes.GuestSatisfactionScore, KpiCodes.Nps, KpiCodes.RepeatGuestRate, KpiCodes.ComplaintRate,
            KpiCodes.DirectBookingRatio, KpiCodes.ChannelCost, KpiCodes.ConversionRate,
            // Achats et stocks
            KpiCodes.InventoryTurnover, KpiCodes.StockOutRate, KpiCodes.PurchasePriceVariance,
            KpiCodes.SupplierOnTimeDeliveryRate, KpiCodes.HousekeepingCostPerRoom, KpiCodes.EnergyCostPerOccupiedRoom,
            KpiCodes.WaterPerGuestNight
        ];

        Assert.Equal(86, historicalOrder.Length);
        Assert.Equal(historicalOrder, KpiCatalog.All.Select(definition => definition.Code).ToArray());
    }

    /// <summary>
    /// Le critere du rattachement : ce qui parle de chambres, de nuitees ou d'etages est du
    /// pack hotelier ; ce qui parle de couverts et de denrees est de la restauration. Un
    /// indicateur qui exige un droit hotelier (hebergement, housekeeping) ne peut pas etre dans
    /// le socle, sinon un commerce le verrait et ne pourrait jamais le lire.
    /// </summary>
    [Fact]
    public void Everything_that_reads_a_room_is_in_the_hospitality_pack()
    {
        string[] hotelPermissions = [PermissionCatalog.LodgingRead, PermissionCatalog.HousekeepingRead];

        Assert.All(KpiCatalog.All, definition =>
        {
            var readsRooms = definition.Category == KpiCategory.Accommodation
                || definition.SourceModule is KpiSourceModule.Lodging or KpiSourceModule.Housekeeping
                || definition.RequiredPermissions.Any(hotelPermissions.Contains);

            if (readsRooms)
            {
                Assert.Equal(ModulePack.Hospitality, definition.Pack);
            }

            if (definition.Category == KpiCategory.FoodBeverage)
            {
                Assert.Equal(ModulePack.FoodBeverage, definition.Pack);
            }
        });
    }

    [Fact]
    public void The_core_packs_never_mention_a_hotel_module()
    {
        ModulePack[] verticalPacks = [ModulePack.Hospitality, ModulePack.FoodBeverage, ModulePack.Events];

        Assert.All(
            KpiCatalog.All.Where(definition => !verticalPacks.Contains(definition.Pack)),
            definition =>
            {
                Assert.NotEqual(KpiCategory.Accommodation, definition.Category);
                Assert.False(definition.SourceModule is KpiSourceModule.Lodging or KpiSourceModule.Housekeeping, definition.Code);
                Assert.DoesNotContain(PermissionCatalog.LodgingRead, definition.RequiredPermissions);
                Assert.DoesNotContain(PermissionCatalog.HousekeepingRead, definition.RequiredPermissions);
            });
    }

    /// <summary>
    /// La repartition declaree : 45 fiches n'existent que pour l'hotellerie et la restauration,
    /// 41 parlent a toute entreprise. Un chiffre qui bouge ici est un choix de produit, pas un
    /// effet de bord.
    /// </summary>
    [Theory]
    [InlineData(ModulePack.Hospitality, 30)]
    [InlineData(ModulePack.FoodBeverage, 15)]
    [InlineData(ModulePack.Finance, 19)]
    [InlineData(ModulePack.HumanResources, 9)]
    [InlineData(ModulePack.Core, 6)]
    [InlineData(ModulePack.Crm, 3)]
    [InlineData(ModulePack.Inventory, 2)]
    [InlineData(ModulePack.Purchasing, 2)]
    [InlineData(ModulePack.Sales, 0)]
    [InlineData(ModulePack.Events, 0)]
    [InlineData(ModulePack.Pilotage, 0)]
    [InlineData(ModulePack.System, 0)]
    public void Each_pack_carries_its_declared_share_of_the_library(ModulePack pack, int expectedCount)
    {
        Assert.Equal(expectedCount, KpiCatalog.All.Count(definition => definition.Pack == pack));
    }

    [Fact]
    public void ForPacks_keeps_only_the_active_packs_in_the_order_of_the_library()
    {
        var everything = Enum.GetValues<ModulePack>().ToHashSet();
        Assert.Equal(KpiCatalog.All, KpiCatalog.ForPacks(everything));

        HashSet<ModulePack> retail =
        [
            ModulePack.Core, ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing,
            ModulePack.Inventory, ModulePack.Crm, ModulePack.Pilotage, ModulePack.System
        ];

        var forRetail = KpiCatalog.ForPacks(retail);

        Assert.Equal(KpiCatalog.All.Count(definition => retail.Contains(definition.Pack)), forRetail.Count);
        Assert.DoesNotContain(forRetail, definition => definition.Category == KpiCategory.Accommodation);
        Assert.DoesNotContain(forRetail, definition => definition.Code == KpiCodes.RevPar);
        Assert.Contains(forRetail, definition => definition.Code == KpiCodes.RevenueTotal);
        Assert.Contains(forRetail, definition => definition.Code == KpiCodes.Nps);

        // Un sous-ensemble ordonne : la position relative de deux fiches ne change jamais.
        var library = KpiCatalog.All.ToList();
        var positions = forRetail.Select(definition => library.IndexOf(definition)).ToArray();
        Assert.Equal(positions.OrderBy(position => position), positions);

        Assert.Empty(KpiCatalog.ForPacks(new HashSet<ModulePack>()));
        Assert.Throws<ArgumentNullException>(() => KpiCatalog.ForPacks(null!));
    }
}
