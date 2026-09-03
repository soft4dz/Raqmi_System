using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Composition pure de « Mon travail » par jeu de clés : lecture compose, action donne le verbe,
/// sinon Suivi ; cible verrouillée à chiffre lisible ; unité du poste absente ; ordre des
/// sections, des slots et des sources.
/// </summary>
public sealed class HomeComposerTests
{
    private static IReadOnlySet<string> Only(params string[] permissions) =>
        permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> NoPermission = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static string[] Ids(HomeSection section) => section.Slots.Select(slot => slot.Queue.Id).ToArray();

    // ----------------------------------------------------------------- registre

    [Fact]
    public void The_registry_holds_thirty_one_queues_with_unique_ids_and_one_route_per_source()
    {
        Assert.Equal(31, HomeWorkQueueCatalog.Queues.Count);
        Assert.Equal(31, HomeWorkQueueCatalog.Queues.Select(queue => queue.Id).Distinct(StringComparer.Ordinal).Count());

        var sources = Enum.GetValues<HomeSource>();
        Assert.Equal(sources.Length, HomeWorkQueueCatalog.Routes.Select(route => route.Source).Distinct().Count());
        Assert.All(sources, source => Assert.Contains(HomeWorkQueueCatalog.Routes, route => route.Source == source));

        // Chaque file appelle une source qui a une route, et sa clé de lecture est celle de la route.
        foreach (var queue in HomeWorkQueueCatalog.Queues)
        {
            var route = Assert.Single(HomeWorkQueueCatalog.Routes, candidate => candidate.Source == queue.Source);
            Assert.Equal(route.ReadKey, queue.ReadKey);
        }
    }

    [Fact]
    public void Overdue_placements_come_from_a_server_flag_or_from_the_three_documented_editorial_choices()
    {
        var overdue = HomeWorkQueueCatalog.Queues.Where(queue => queue.Band == HomeBand.Overdue).ToArray();

        Assert.All(overdue, queue => Assert.NotEqual(HomeBandBasis.Registry, queue.BandBasis));

        var editorial = overdue.Where(queue => queue.BandBasis == HomeBandBasis.Editorial).Select(queue => queue.Id).Order(StringComparer.Ordinal);
        Assert.Equal(["aging-90", "dec-backlog", "dec-rejected"], editorial);

        var serverFlag = overdue.Where(queue => queue.BandBasis == HomeBandBasis.ServerFlag).Select(queue => queue.Id).Order(StringComparer.Ordinal);
        Assert.Equal(["arrivals-late", "backup", "closing-unit", "departures-late"], serverFlag);

        // Hors « En retard », la bande est toujours celle du registre.
        Assert.All(
            HomeWorkQueueCatalog.Queues.Where(queue => queue.Band != HomeBand.Overdue),
            queue => Assert.Equal(HomeBandBasis.Registry, queue.BandBasis));
    }

    [Fact]
    public void The_approvals_queue_reads_with_the_decide_key_never_with_approvals_read()
    {
        var approvals = HomeWorkQueueCatalog.Find("approvals");

        Assert.Equal(PermissionCatalog.WorkflowRequestDecide, approvals.ReadKey);
        Assert.Equal(HomeScope.Me, approvals.Scope);
        Assert.DoesNotContain(PermissionCatalog.ApprovalsRead, PermissionRegistry.AcceptedClaims(approvals.ReadKey));
    }

    // ------------------------------------------------------------- composition

    [Fact]
    public void Sections_are_rendered_in_a_fixed_order()
    {
        var layout = HomeComposer.Compose(NoPermission, hasStationUnit: false);

        Assert.Equal(
            [HomeSectionKind.Banner, HomeSectionKind.Overdue, HomeSectionKind.Today, HomeSectionKind.Watch, HomeSectionKind.RecentScreens, HomeSectionKind.Product],
            layout.Sections.Select(section => section.Kind));
    }

    [Fact]
    public void A_read_key_alone_composes_its_queues_in_watch_mode()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.InventoryRead), hasStationUnit: false);

        var counts = Assert.Single(layout.Band(HomeBand.Today).Slots);
        Assert.Equal("counts-draft", counts.Queue.Id);
        Assert.Equal(HomeMode.Watch, counts.Mode);
        Assert.False(counts.TargetLocked);
        Assert.Equal(24, counts.TargetTab);

        var lowStock = Assert.Single(layout.Band(HomeBand.Watch).Slots);
        Assert.Equal("low-stock", lowStock.Queue.Id);
        Assert.Equal(HomeMode.Information, lowStock.Mode);

        Assert.Equal([HomeSource.LowStock, HomeSource.InventoryCountsDraft], layout.Sources);
        Assert.True(layout.WatchOnly);
    }

    [Fact]
    public void The_action_key_turns_the_same_queue_into_act_mode()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.InventoryRead, PermissionCatalog.InventoryValidate), hasStationUnit: false);

        var counts = Assert.Single(layout.Band(HomeBand.Today).Slots);
        Assert.Equal(HomeMode.Act, counts.Mode);
        Assert.False(layout.WatchOnly);
    }

    [Fact]
    public void Target_keys_accept_the_covering_legacy_key_and_the_target_key_alike()
    {
        var legacy = HomeComposer.Compose(Only(PermissionCatalog.InventoryRead, PermissionCatalog.InventoryValidate), false);
        var target = HomeComposer.Compose(Only(PermissionCatalog.InventoryStockRead, PermissionCatalog.InventoryCountValidate), false);

        Assert.Equal(Ids(legacy.Band(HomeBand.Today)), Ids(target.Band(HomeBand.Today)));
        Assert.Equal(legacy.Band(HomeBand.Today).Slots[0].Mode, target.Band(HomeBand.Today).Slots[0].Mode);
    }

    [Fact]
    public void Approvals_read_alone_composes_no_approvals_queue_and_plans_no_pending_call()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.ApprovalsRead), hasStationUnit: false);

        Assert.DoesNotContain(layout.Slots, slot => slot.Queue.Id == "approvals");
        Assert.DoesNotContain(HomeSource.PendingApprovals, layout.Sources);
        Assert.Equal(HomeEmptyReason.NoQueues, layout.Band(HomeBand.Today).EmptyReason);
    }

    [Theory]
    [InlineData(PermissionCatalog.ApprovalsDecide)]
    [InlineData(PermissionCatalog.WorkflowRequestDecide)]
    public void The_decide_key_composes_the_approvals_queue_under_its_legacy_or_target_name(string key)
    {
        var layout = HomeComposer.Compose(Only(key), hasStationUnit: false);

        var slot = Assert.Single(layout.Slots);
        Assert.Equal("approvals", slot.Queue.Id);
        Assert.Equal(HomeMode.Act, slot.Mode);
        Assert.Equal(HomeScope.Me, slot.Scope);
        Assert.Equal([HomeSource.PendingApprovals], layout.Sources);

        // approvals.read manque : l'onglet Validations est fermé, le chiffre reste lisible.
        Assert.True(slot.TargetLocked);
        Assert.Equal(16, slot.TargetTab);
    }

    [Fact]
    public void Lodging_read_without_a_station_unit_composes_nothing_and_asks_for_the_unit()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.LodgingRead), hasStationUnit: false);

        Assert.Empty(layout.Slots);
        Assert.True(layout.UnitQueuesSkipped > 0);
        Assert.True(layout.ShowUnitMissingBanner);
        Assert.True(layout.ShowUnitLine);
        Assert.False(layout.ShowBusinessDate);
        Assert.Equal(HomeEmptyReason.UnitMissing, layout.Band(HomeBand.Overdue).EmptyReason);
        Assert.Equal(HomeEmptyReason.UnitMissing, layout.Band(HomeBand.Today).EmptyReason);
        Assert.Equal(HomeEmptyReason.NoQueues, layout.Band(HomeBand.Watch).EmptyReason);
        Assert.Empty(layout.Sources);
    }

    [Fact]
    public void Lodging_read_with_a_station_unit_composes_the_front_desk_queues_and_reads_the_business_date()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.LodgingRead), hasStationUnit: true);

        Assert.Equal(["arrivals-late", "departures-late", "closing-unit"], Ids(layout.Band(HomeBand.Overdue)));
        Assert.Equal(["arrivals", "arrivals-unassigned", "departures", "departures-balance"], Ids(layout.Band(HomeBand.Today)));
        Assert.All(layout.Slots, slot => Assert.Equal(HomeMode.Watch, slot.Mode));
        Assert.All(layout.Slots, slot => Assert.Equal(HomeScope.Unit, slot.Scope));

        Assert.True(layout.ShowBusinessDate);
        Assert.False(layout.ShowUnitMissingBanner);
        Assert.Equal(0, layout.UnitQueuesSkipped);

        // La clôture (onglet 5) est fermée sans closing.read : repli sur le PMS (30).
        var closing = layout.Slots.Single(slot => slot.Queue.Id == "closing-unit");
        Assert.Equal(30, closing.TargetTab);
        Assert.False(closing.TargetLocked);

        Assert.Equal([HomeSource.BusinessDate, HomeSource.FrontDesk, HomeSource.ArrivalBoard, HomeSource.DepartureBoard], layout.Sources);
    }

    [Fact]
    public void Settings_read_alone_composes_no_queue_and_reports_no_queues_on_every_band()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.SettingsRead), hasStationUnit: true);

        Assert.Empty(layout.Slots);
        Assert.Empty(layout.Sources);
        Assert.False(layout.ShowUnitLine);
        Assert.False(layout.ShowUnitMissingBanner);
        Assert.False(layout.WatchOnly);
        Assert.True(layout.ShowEstablishment);
        Assert.True(layout.CanOpenSettings);

        Assert.All(
            new[] { HomeBand.Overdue, HomeBand.Today, HomeBand.Watch },
            band => Assert.Equal(HomeEmptyReason.NoQueues, layout.Band(band).EmptyReason));
    }

    [Fact]
    public void A_closed_target_with_an_openable_fallback_switches_to_the_fallback()
    {
        // dashboard.read sans treasury.read : la carte OP porte le chiffre DEC et ouvre le cockpit.
        var layout = HomeComposer.Compose(Only(PermissionCatalog.DashboardRead), hasStationUnit: false);

        var po = layout.Slots.Single(slot => slot.Queue.Id == "dec-po");
        Assert.Equal(20, po.TargetTab);
        Assert.False(po.TargetLocked);
        Assert.Equal(HomeMode.Watch, po.Mode);

        var revenue = layout.Slots.Single(slot => slot.Queue.Id == "dec-revenue");
        Assert.Equal(20, revenue.TargetTab);

        var backlog = layout.Slots.Single(slot => slot.Queue.Id == "dec-backlog");
        Assert.Equal(20, backlog.TargetTab);
        Assert.False(backlog.TargetLocked);
    }

    [Fact]
    public void A_closed_target_without_fallback_keeps_the_figure_and_locks_the_button()
    {
        // La clé cible du housekeeping sans housekeeping.read historique : l'onglet 21 est gardé
        // par ApplyModuleAccess(HousekeepingRead), dont AcceptedClaims accepte la cible — donc
        // ouvrable. Le cas verrouillé se produit quand la clé de lecture de la file n'est pas
        // celle de l'onglet : la file « approvals » (décision) sans approvals.read.
        var layout = HomeComposer.Compose(Only(PermissionCatalog.WorkflowRequestDecide), hasStationUnit: false);

        var approvals = Assert.Single(layout.Slots);
        Assert.True(approvals.TargetLocked);
        Assert.Equal(approvals.Queue.TargetTab, approvals.TargetTab);
    }

    [Fact]
    public void Slots_of_a_band_are_ordered_act_then_watch_then_information_then_registry()
    {
        var layout = HomeComposer.Compose(
            Only(PermissionCatalog.DashboardRead, PermissionCatalog.TreasuryRead, PermissionCatalog.TreasuryApprove, PermissionCatalog.ReceivablesRead),
            hasStationUnit: false);

        // Aujourd'hui : dec-po (Act), dec-revenue / po-pay / receipts-draft (Watch), puis les
        // informations revenue-yesterday / receipts-today, dans l'ordre du registre.
        Assert.Equal(
            ["dec-po", "dec-revenue", "po-pay", "receipts-draft", "revenue-yesterday", "receipts-today"],
            Ids(layout.Band(HomeBand.Today)));

        Assert.Equal(["dec-backlog", "dec-rejected", "aging-90"], Ids(layout.Band(HomeBand.Overdue)));
    }

    [Fact]
    public void Sources_are_deduplicated_and_ordered_from_lightest_to_heaviest()
    {
        var layout = HomeComposer.Compose(Only(PermissionCatalog.DashboardRead, PermissionCatalog.TreasuryRead), hasStationUnit: false);

        // Quatre cartes lisent le cockpit DEC, deux la synthèse des encaissements : une source
        // chacune, et le cockpit (le plus lourd) ferme la marche.
        Assert.Equal(layout.Sources.Distinct().Count(), layout.Sources.Count);
        Assert.Equal(HomeSource.DecCockpit, layout.Sources[^1]);
        Assert.Equal(layout.Sources.OrderBy(source => source), layout.Sources);
        Assert.Contains(HomeSource.ReceiptsDraft, layout.Sources);
        Assert.Contains(HomeSource.ReceiptsConfirmed, layout.Sources);
    }

    [Fact]
    public void Receipts_queues_take_the_station_unit_when_known_and_the_group_otherwise()
    {
        var group = HomeComposer.Compose(Only(PermissionCatalog.TreasuryRead), hasStationUnit: false);
        var unit = HomeComposer.Compose(Only(PermissionCatalog.TreasuryRead), hasStationUnit: true);

        Assert.Equal(HomeScope.Group, group.Slots.Single(slot => slot.Queue.Id == "receipts-draft").Scope);
        Assert.Equal(HomeScope.Unit, unit.Slots.Single(slot => slot.Queue.Id == "receipts-draft").Scope);

        // Ces deux files ne sont jamais ignorées faute d'unité : aucun encart n'est demandé.
        Assert.False(group.ShowUnitMissingBanner);
        Assert.False(group.ShowUnitLine);
    }

    [Fact]
    public void The_reception_profile_of_the_specification_composes_five_sources()
    {
        var reception = Only(
            PermissionCatalog.LodgingRead, PermissionCatalog.LodgingCheckin, PermissionCatalog.LodgingCheckout,
            PermissionCatalog.LodgingReserve, PermissionCatalog.LodgingRoomMove, PermissionCatalog.CustomersRead,
            PermissionCatalog.CrmRead, PermissionCatalog.HousekeepingRead, PermissionCatalog.SettingsRead);

        var layout = HomeComposer.Compose(reception, hasStationUnit: true);

        Assert.Equal(["arrivals-late", "departures-late", "closing-unit"], Ids(layout.Band(HomeBand.Overdue)));
        Assert.Equal(HomeMode.Act, layout.Band(HomeBand.Overdue).Slots[0].Mode);
        Assert.Equal(HomeMode.Watch, layout.Band(HomeBand.Overdue).Slots[2].Mode);

        Assert.Equal(
            ["arrivals", "arrivals-unassigned", "departures", "departures-balance", "hk-dirty", "hk-inspect"],
            Ids(layout.Band(HomeBand.Today)));

        Assert.Equal(["hk-ooo"], Ids(layout.Band(HomeBand.Watch)));

        Assert.Equal(
            [HomeSource.BusinessDate, HomeSource.FrontDesk, HomeSource.ArrivalBoard, HomeSource.DepartureBoard, HomeSource.HousekeepingBoard],
            layout.Sources);
    }
}
