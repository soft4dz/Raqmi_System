using System.Globalization;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Tests;

/// <summary>
/// Mise en bande des cartes après projection : la bande affichée est celle que la carte porte,
/// jamais celle que le registre déclarait avant l'appel — sinon l'en-tête d'une bande
/// contredirait la synthèse du bandeau sur le même écran.
/// </summary>
public sealed class HomeBandPlacementTests
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static HomeSlot SlotOf(string id, HomeMode mode = HomeMode.Act)
    {
        var queue = HomeWorkQueueCatalog.Find(id);

        return new HomeSlot(queue, mode, queue.Scope, queue.TargetTab, false);
    }

    private static HomeCard Backup(bool isOverdue)
    {
        var results = new HomeSourceResults
        {
            BackupStatus = new BackupStatusResponse(
                true,
                "D:\\backups",
                12,
                new BackupFileResponse("raqmi-2026-09-01.bak", "daily", 1024, DateTimeOffset.UtcNow),
                isOverdue ? 49.6 : 3.2,
                isOverdue,
                26)
        };

        return HomeProjection.Project(SlotOf("backup"), results, "DA", Fr);
    }

    [Fact]
    public void A_backup_the_server_declares_on_time_leaves_the_overdue_band()
    {
        // Le registre range « backup » dans « En retard » ; IsOverdue = false la déplace.
        Assert.Equal(HomeBand.Overdue, HomeWorkQueueCatalog.Find("backup").Band);

        var card = Backup(isOverdue: false);
        HomeCard[] cards = [card];

        Assert.Empty(HomeBandPlacement.InBand(cards, HomeBand.Overdue));
        Assert.Empty(HomeBandPlacement.InBand(cards, HomeBand.Today));

        var watch = HomeBandPlacement.InBand(cards, HomeBand.Watch);
        Assert.Single(watch);
        Assert.Equal("Dernière sauvegarde", watch[0].Label);
    }

    [Fact]
    public void A_backup_the_server_declares_overdue_stays_in_the_overdue_band()
    {
        HomeCard[] cards = [Backup(isOverdue: true)];

        var overdue = HomeBandPlacement.InBand(cards, HomeBand.Overdue);
        Assert.Single(overdue);
        Assert.Equal("Sauvegarde en retard", overdue[0].Label);
        Assert.Empty(HomeBandPlacement.InBand(cards, HomeBand.Watch));
    }

    [Fact]
    public void A_hidden_card_belongs_to_no_band()
    {
        var hidden = Card("hk-ooo", HomeBand.Watch, HomeMode.Information, isHidden: true);
        HomeCard[] cards = [hidden];

        foreach (var band in Enum.GetValues<HomeBand>())
        {
            Assert.Empty(HomeBandPlacement.InBand(cards, band));
        }
    }

    [Fact]
    public void A_band_is_ordered_act_then_watch_then_information()
    {
        // Volontairement fournies dans le désordre : la répartition ne doit rien devoir à
        // l'ordre d'énumération du dictionnaire de la vue.
        HomeCard[] cards =
        [
            Card("revenue-yesterday", HomeBand.Today, HomeMode.Information),
            Card("arrivals", HomeBand.Today, HomeMode.Watch),
            Card("departures", HomeBand.Today, HomeMode.Act)
        ];

        Assert.Equal(
            ["departures", "arrivals", "revenue-yesterday"],
            HomeBandPlacement.InBand(cards, HomeBand.Today).Select(card => card.Slot.Queue.Id));
    }

    [Fact]
    public void At_equal_mode_a_band_keeps_the_order_of_the_registry()
    {
        HomeCard[] cards =
        [
            Card("departures", HomeBand.Today, HomeMode.Act),
            Card("arrivals", HomeBand.Today, HomeMode.Act)
        ];

        Assert.Equal(
            ["arrivals", "departures"],
            HomeBandPlacement.InBand(cards, HomeBand.Today).Select(card => card.Slot.Queue.Id));
    }

    private static HomeCard Card(string id, HomeBand band, HomeMode mode, bool isHidden = false)
    {
        var slot = SlotOf(id, mode);

        return new HomeCard(slot, slot.Queue.Label, band, HomeCardState.Ready, "1", null, "légende", false, isHidden);
    }
}
