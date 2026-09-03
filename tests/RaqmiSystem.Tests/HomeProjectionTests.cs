using System.Globalization;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Projection pure des réponses serveur vers les cartes : bande imposée par un booléen serveur,
/// zéros masqués ou gardés selon la bande, source en échec, montants lus tels quels.
/// </summary>
public sealed class HomeProjectionTests
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static HomeSlot SlotOf(string id, HomeMode mode = HomeMode.Act) =>
        new(HomeWorkQueueCatalog.Find(id), mode, HomeWorkQueueCatalog.Find(id).Scope, HomeWorkQueueCatalog.Find(id).TargetTab, false);

    // Seuls les effectifs des listes sont lus par la projection : des tableaux de la bonne
    // taille suffisent, leurs lignes ne sont jamais déréférencées.
    private static FrontDeskResponse FrontDesk(int arrivals, int overdueArrivals, int departures = 0, int overdueDepartures = 0) =>
        new(
            "ALG-CEN",
            new DateOnly(2026, 9, 1),
            new FrontDeskArrivalResponse[arrivals],
            new FrontDeskArrivalResponse[overdueArrivals],
            new FrontDeskDepartureResponse[departures],
            new FrontDeskDepartureResponse[overdueDepartures],
            31,
            new OccupancyDayResponse(new DateOnly(2026, 9, 1), 100, 74, 74m));

    [Fact]
    public void A_source_that_has_not_answered_yet_gives_loading_cards()
    {
        var card = HomeProjection.Project(SlotOf("arrivals"), new HomeSourceResults(), "DA", Fr);

        Assert.Equal(HomeCardState.Loading, card.State);
        Assert.False(card.IsHidden);
    }

    [Fact]
    public void A_failed_source_turns_all_its_cards_unavailable()
    {
        var results = new HomeSourceResults();
        results.Failed.Add(HomeSource.FrontDesk);

        foreach (var id in new[] { "arrivals", "arrivals-late", "departures", "departures-late" })
        {
            var card = HomeProjection.Project(SlotOf(id), results, "DA", Fr);
            Assert.Equal(HomeCardState.Unavailable, card.State);
            Assert.Equal("—", card.CountText);
            Assert.False(card.IsHidden);
        }
    }

    [Fact]
    public void Overdue_arrivals_at_zero_are_hidden_while_today_arrivals_at_zero_stay_visible()
    {
        var results = new HomeSourceResults { FrontDesk = FrontDesk(arrivals: 0, overdueArrivals: 0) };

        var late = HomeProjection.Project(SlotOf("arrivals-late"), results, "DA", Fr);
        Assert.True(late.IsZero);
        Assert.True(late.IsHidden);
        Assert.Equal(HomeBand.Overdue, late.Band);

        var today = HomeProjection.Project(SlotOf("arrivals"), results, "DA", Fr);
        Assert.True(today.IsZero);
        Assert.False(today.IsHidden);
        Assert.Equal("0", today.CountText);
        Assert.Contains("31 clients présents", today.Legend);
        Assert.Contains("74 %", today.Legend);
    }

    [Fact]
    public void Overdue_arrivals_are_counted_from_the_server_list_not_from_a_client_threshold()
    {
        var results = new HomeSourceResults { FrontDesk = FrontDesk(arrivals: 14, overdueArrivals: 2, departures: 9, overdueDepartures: 1) };

        Assert.Equal("2", HomeProjection.Project(SlotOf("arrivals-late"), results, "DA", Fr).CountText);
        Assert.Equal("1", HomeProjection.Project(SlotOf("departures-late"), results, "DA", Fr).CountText);
        Assert.Equal("14", HomeProjection.Project(SlotOf("arrivals"), results, "DA", Fr).CountText);
        Assert.Equal("9", HomeProjection.Project(SlotOf("departures"), results, "DA", Fr).CountText);
        Assert.Null(HomeProjection.Project(SlotOf("departures-late"), results, "DA", Fr).AmountText);
    }

    [Fact]
    public void The_closing_card_exists_only_when_the_server_says_the_unit_is_late()
    {
        var onTime = new HomeSourceResults
        {
            BusinessDate = new BusinessDateResponse("ALG-CEN", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 31), true, IsLate: false, 0)
        };

        Assert.True(HomeProjection.Project(SlotOf("closing-unit"), onTime, "DA", Fr).IsHidden);

        var late = new HomeSourceResults
        {
            BusinessDate = new BusinessDateResponse("ALG-CEN", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 30), true, IsLate: true, 1)
        };

        var card = HomeProjection.Project(SlotOf("closing-unit"), late, "DA", Fr);
        Assert.False(card.IsHidden);
        Assert.Equal("1", card.CountText);
        Assert.Equal("Journée à clôturer", card.Label);
        Assert.Contains("30/08/2026", card.Legend);
        Assert.Equal(HomeBand.Overdue, card.Band);
    }

    [Fact]
    public void The_backup_card_moves_between_overdue_and_watch_on_the_server_flag()
    {
        var overdue = new HomeSourceResults
        {
            BackupStatus = new BackupStatusResponse(true, "D:\\backups", 12, new BackupFileResponse("raqmi-2026-08-30.bak", "daily", 1024, DateTimeOffset.UtcNow), 49.6, IsOverdue: true, 26)
        };

        var late = HomeProjection.Project(SlotOf("backup"), overdue, "DA", Fr);
        Assert.Equal(HomeBand.Overdue, late.Band);
        Assert.Equal("Sauvegarde en retard", late.Label);
        Assert.Equal("50 h", late.CountText);
        Assert.False(late.IsHidden);

        var fresh = new HomeSourceResults
        {
            BackupStatus = new BackupStatusResponse(true, "D:\\backups", 12, new BackupFileResponse("raqmi-2026-09-01.bak", "daily", 1024, DateTimeOffset.UtcNow), 3.2, IsOverdue: false, 26)
        };

        var ok = HomeProjection.Project(SlotOf("backup"), fresh, "DA", Fr);
        Assert.Equal(HomeBand.Watch, ok.Band);
        Assert.Equal("Dernière sauvegarde", ok.Label);
        Assert.False(ok.IsHidden);
        Assert.False(ok.IsZero);
    }

    [Fact]
    public void Amounts_are_shown_only_where_the_server_returns_an_aggregate()
    {
        var results = new HomeSourceResults
        {
            DepartureBoard = new DepartureBoardResponse("ALG-CEN", new DateOnly(2026, 9, 1), [], 2, 27400m),
            Aging = new AgingBalanceResponse(new DateOnly(2026, 9, 1), "all", "invoice", [], new AgingBucketsResponse(10m, 20m, 30m, 40m, 1234.5m, 1334.5m)),
            UnitDashboardYesterday = new UnitDashboardResponse(new DateOnly(2026, 8, 31), [], 4, 3, 1, 2, 987654.32m)
        };

        var departures = HomeProjection.Project(SlotOf("departures-balance"), results, "DA", Fr);
        Assert.Equal("2", departures.CountText);
        Assert.Equal(HomeProjection.FormatAmount(27400m, "DA", Fr), departures.AmountText);

        var aging = HomeProjection.Project(SlotOf("aging-90"), results, "DA", Fr);
        Assert.Equal(HomeProjection.FormatAmount(1234.5m, "DA", Fr), aging.CountText);
        Assert.Null(aging.AmountText);
        Assert.Contains("01/09/2026", aging.Legend);

        var yesterday = HomeProjection.Project(SlotOf("revenue-yesterday"), results, "DA", Fr);
        Assert.Equal(HomeProjection.FormatAmount(987654.32m, "DA", Fr), yesterday.CountText);
        Assert.Contains("3/4 unités saisies", yesterday.Legend);
        Assert.False(yesterday.IsZero);
    }

    [Fact]
    public void The_grand_total_of_receipts_is_read_only_from_the_confirmed_summary()
    {
        var draft = new CashReceiptSummaryResponse(null, null, null, ReceiptStatus.Draft, 5, 5, 0, 0, 0, 0, 0, 0, 99999m);
        var confirmed = new CashReceiptSummaryResponse(null, null, null, ReceiptStatus.Confirmed, 7, 0, 7, 0, 100m, 200m, 0, 0, 300m);

        var results = new HomeSourceResults { ReceiptsDraft = draft, ReceiptsConfirmed = confirmed };

        var draftCard = HomeProjection.Project(SlotOf("receipts-draft"), results, "DA", Fr);
        Assert.Equal("5", draftCard.CountText);
        Assert.Null(draftCard.AmountText);
        Assert.DoesNotContain("99", draftCard.CountText);

        var todayCard = HomeProjection.Project(SlotOf("receipts-today"), results, "DA", Fr);
        Assert.Equal(HomeProjection.FormatAmount(300m, "DA", Fr), todayCard.CountText);
        Assert.Contains("7 encaissements", todayCard.Legend);
    }

    [Fact]
    public void Purchase_orders_to_receive_count_the_server_flag_not_the_status()
    {
        PurchaseOrderResponse Order(bool canReceive) =>
            new(Guid.NewGuid(), "PO-1", "SUP", "Fournisseur", "WH", new DateOnly(2026, 9, 1), PurchaseOrderStatus.Approved,
                100m, 10m, 0m, [], false, canReceive, null, null, null, null, null, DateTimeOffset.UtcNow, "x", null, null);

        var results = new HomeSourceResults
        {
            PurchaseOrdersApproved = [Order(true), Order(false), Order(true)]
        };

        Assert.Equal("2", HomeProjection.Project(SlotOf("po-receive"), results, "DA", Fr).CountText);
    }

    [Fact]
    public void Housekeeping_cards_share_one_board_and_hide_their_zeros_outside_today()
    {
        var results = new HomeSourceResults
        {
            HousekeepingBoard = new RoomBoardResponse("ALG-CEN", new DateOnly(2026, 9, 1), 100, 60, 6, 30, 0, 9, 14, 5, 74, 26, 6, 2, 3, [])
        };

        var dirty = HomeProjection.Project(SlotOf("hk-dirty"), results, "DA", Fr);
        Assert.Equal("6", dirty.CountText);
        Assert.Contains("60 propres sur 100", dirty.Legend);

        Assert.Equal("3", HomeProjection.Project(SlotOf("hk-inspect"), results, "DA", Fr).CountText);

        var ooo = HomeProjection.Project(SlotOf("hk-ooo"), results, "DA", Fr);
        Assert.True(ooo.IsZero);
        Assert.True(ooo.IsHidden);
    }

    [Fact]
    public void Every_queue_of_the_registry_has_a_projection()
    {
        // Une source vide mais « chargée » suffit à traverser chaque branche sans exception
        // lorsqu'on marque la source en échec : la projection ne lit alors rien.
        var failed = new HomeSourceResults();

        foreach (var source in Enum.GetValues<HomeSource>())
        {
            failed.Failed.Add(source);
        }

        foreach (var queue in HomeWorkQueueCatalog.Queues)
        {
            var slot = new HomeSlot(queue, HomeMode.Information, queue.Scope, queue.TargetTab, false);
            var card = HomeProjection.Project(slot, failed, null, Fr);
            Assert.Equal(HomeCardState.Unavailable, card.State);
            Assert.Equal(queue.Label, card.Label);
        }

        Assert.Equal(PermissionCatalog.WorkflowRequestDecide, HomeWorkQueueCatalog.Find("approvals").ActKey);
    }
}
