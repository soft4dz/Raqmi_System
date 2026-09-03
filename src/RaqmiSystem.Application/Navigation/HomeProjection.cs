using System.Globalization;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.Navigation;

/// <summary>État d'une carte de file de travail.</summary>
public enum HomeCardState
{
    Loading,
    Ready,
    Unavailable
}

/// <summary>
/// Une carte prête à être affichée : ce que la projection a lu dans les réponses serveur.
/// </summary>
/// <param name="CountText">Le compteur principal : un entier, ou un montant formaté quand le serveur renvoie l'agrégat.</param>
/// <param name="AmountText">Montant secondaire formaté, seulement quand le serveur renvoie l'agrégat.</param>
/// <param name="IsZero">Le compteur vaut zéro : masqué hors d'« Aujourd'hui », atténué dedans.</param>
/// <param name="IsHidden">La carte ne s'affiche pas (zéro hors d'Aujourd'hui, journée à clôturer sans retard).</param>
public sealed record HomeCard(
    HomeSlot Slot,
    string Label,
    HomeBand Band,
    HomeCardState State,
    string CountText,
    string? AmountText,
    string Legend,
    bool IsZero,
    bool IsHidden);

/// <summary>
/// Projection pure des réponses serveur vers les cartes : aucun seuil, aucune addition, aucun
/// calcul de montant côté client. La bande finale est celle du registre, ou celle qu'un booléen
/// serveur impose (<c>IsLate</c>, <c>IsOverdue</c>).
/// </summary>
public static class HomeProjection
{
    private const string UnavailableCount = "—";
    private const string UnavailableLegend = "F5 pour réessayer · détail dans le bandeau de session";

    public static HomeCard Project(HomeSlot slot, HomeSourceResults results, string? currencyLabel, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(results);

        var queue = slot.Queue;
        culture ??= CultureInfo.CurrentCulture;

        if (results.Failed.Contains(queue.Source))
        {
            return new HomeCard(slot, queue.Label, queue.Band, HomeCardState.Unavailable, UnavailableCount, null, UnavailableLegend, false, false);
        }

        if (!results.IsLoaded(queue.Source))
        {
            return new HomeCard(slot, queue.Label, queue.Band, HomeCardState.Loading, string.Empty, null, string.Empty, false, false);
        }

        var reading = Read(queue.Id, results, currencyLabel, culture);
        var band = reading.Band ?? queue.Band;

        // Les zéros : « En retard » et « À surveiller » masquent une carte à zéro ; « Aujourd'hui »
        // la garde, atténuée (« 0 arrivée aujourd'hui » est une information).
        var hidden = reading.Hidden || (reading.IsZero && band != HomeBand.Today);

        return new HomeCard(
            slot,
            reading.Label ?? queue.Label,
            band,
            HomeCardState.Ready,
            reading.CountText,
            reading.AmountText,
            reading.Legend,
            reading.IsZero,
            hidden);
    }

    /// <summary>Montant tel que le serveur l'a agrégé, formaté N2 dans la culture du poste, suivi du libellé de devise.</summary>
    public static string FormatAmount(decimal amount, string? currencyLabel, CultureInfo? culture = null)
    {
        var text = amount.ToString("N2", culture ?? CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(currencyLabel) ? text : $"{text} {currencyLabel.Trim()}";
    }

    private readonly record struct Reading(
        string CountText,
        string Legend,
        bool IsZero,
        string? AmountText = null,
        HomeBand? Band = null,
        string? Label = null,
        bool Hidden = false);

    private static Reading Count(int count, string legend, string? amountText = null, HomeBand? band = null, string? label = null, bool hidden = false) =>
        new(count.ToString(CultureInfo.InvariantCulture), legend, count == 0, amountText, band, label, hidden);

    private static Reading Read(string id, HomeSourceResults r, string? currency, CultureInfo culture)
    {
        switch (id)
        {
            // ------------------------------------------------------------- En retard
            case "arrivals-late":
                return Count(r.FrontDesk!.OverdueArrivals.Count, "arrivées attendues non enregistrées, candidates au no-show");

            case "departures-late":
                return Count(r.FrontDesk!.OverdueDepartures.Count, "date de départ dépassée, séjour encore ouvert");

            case "closing-unit":
            {
                var date = r.BusinessDate!;
                var legend = date.LastClosedDate is { } closed
                    ? $"dernière clôture le {closed.ToString("dd/MM/yyyy", culture)}"
                    : "aucune clôture enregistrée pour cette unité";

                // IsLate vient du serveur : sans retard, la carte n'existe pas — la date métier du
                // bandeau porte déjà la pastille « à jour ».
                return Count(
                    date.PendingDays,
                    legend,
                    label: date.PendingDays == 1 ? "Journée à clôturer" : "Journées à clôturer",
                    hidden: !date.IsLate);
            }

            case "dec-backlog":
            {
                var dec = r.DecCockpit!;
                var legend = dec.OldestClosingDelay is { } oldest
                    ? $"la plus ancienne : {oldest.HotelUnitCode} · {oldest.BusinessDate.ToString("dd/MM/yyyy", culture)} · {oldest.AgeDays} j"
                    : "toutes unités confondues";
                return Count(dec.ClosingBacklogDayCount, legend);
            }

            case "dec-rejected":
                return Count(r.DecCockpit!.RejectedCount, "recettes rejetées par la DEC, à reprendre par l'unité");

            case "aging-90":
            {
                var aging = r.Aging!;
                return new Reading(
                    FormatAmount(aging.Total.Over90, currency, culture),
                    $"sur {FormatAmount(aging.Total.Total, currency, culture)} de créances ouvertes · au {aging.AsOfDate.ToString("dd/MM/yyyy", culture)}",
                    aging.Total.Over90 == 0);
            }

            case "backup":
            {
                var backup = r.BackupStatus!;
                var age = backup.AgeHours is { } hours ? $"{Math.Round(hours).ToString("N0", culture)} h" : UnavailableCount;

                if (backup.IsOverdue)
                {
                    return new Reading(
                        age,
                        $"seuil {backup.OverdueThresholdHours.ToString("N0", culture)} h · dernière : {backup.LastBackup?.FileName ?? "aucune"}",
                        IsZero: false,
                        Band: HomeBand.Overdue,
                        Label: "Sauvegarde en retard");
                }

                var legend = !backup.Configured
                    ? "sauvegarde non configurée sur le serveur"
                    : backup.LastBackup is { } last
                        ? $"{last.FileName} · {backup.BackupCount} sauvegardes conservées"
                        : "aucune sauvegarde enregistrée";

                return new Reading(age, legend, IsZero: false, Band: HomeBand.Watch, Label: "Dernière sauvegarde");
            }

            // ------------------------------------------------------------ Aujourd'hui
            case "arrivals":
            {
                var desk = r.FrontDesk!;
                return Count(
                    desk.Arrivals.Count,
                    $"{desk.InHouseCount} clients présents · occupation {desk.Occupancy.OccupancyRatePercent.ToString("N0", culture)} %");
            }

            case "arrivals-unassigned":
            {
                var board = r.ArrivalBoard!;
                return Count(board.UnassignedCount, $"{board.RoomsToPrepare} chambres à préparer · {board.NotReadyCount} pas encore prêtes");
            }

            case "departures":
                return Count(r.FrontDesk!.Departures.Count, "départs attendus sur la date métier");

            case "departures-balance":
            {
                var board = r.DepartureBoard!;
                return Count(board.PendingCount, "solde à encaisser avant le départ", FormatAmount(board.OutstandingBalance, currency, culture));
            }

            case "hk-dirty":
            {
                var board = r.HousekeepingBoard!;
                return Count(board.DirtyRooms, $"{board.CleanRooms} propres sur {board.TotalRooms} chambres");
            }

            case "hk-inspect":
                return Count(r.HousekeepingBoard!.AwaitingInspectionTasks, "tâches en attente de contrôle");

            case "approvals":
                return Count(r.PendingApprovals!.Count, "ordres de paiement — seul sujet en circuit aujourd'hui");

            case "dec-revenue":
            {
                var dec = r.DecCockpit!;
                var units = dec.PendingValidations.Count;
                return Count(
                    dec.PendingValidationCount,
                    units == 1 ? "sur une unité" : $"sur {units} unités",
                    FormatAmount(dec.PendingValidationAmount, currency, culture));
            }

            case "dec-po":
            {
                var dec = r.DecCockpit!;
                return Count(dec.PendingPaymentOrderCount, "ordres de paiement en attente d'approbation", FormatAmount(dec.PendingPaymentOrderAmount, currency, culture));
            }

            case "revenue-yesterday":
            {
                var dashboard = r.UnitDashboardYesterday!;
                return new Reading(
                    FormatAmount(dashboard.GrandTotal, currency, culture),
                    $"{dashboard.UnitsWithEntry}/{dashboard.TotalUnits} unités saisies · {dashboard.UnitsMissing} manquantes · {dashboard.UnitsPendingValidation} à valider",
                    dashboard.GrandTotal == 0 && dashboard.UnitsWithEntry == 0);
            }

            case "revenue-draft":
                return Count(r.RevenueSummary!.DraftCount, "recettes de la veille et du jour de l'unité du poste");

            case "po-pay":
                return Count(r.PaymentOrdersApproved!.Count, "approuvés, en attente de règlement");

            case "receipts-draft":
                return Count(r.ReceiptsDraft!.DraftCount, "encaissements du jour non confirmés");

            case "receipts-today":
            {
                // Seule la synthèse filtrée sur status=Confirmed a un GrandTotal documenté.
                var receipts = r.ReceiptsConfirmed!;
                return new Reading(
                    FormatAmount(receipts.GrandTotal, currency, culture),
                    receipts.ConfirmedCount == 1 ? "1 encaissement confirmé" : $"{receipts.ConfirmedCount} encaissements confirmés",
                    receipts.ConfirmedCount == 0);
            }

            case "counts-draft":
                return Count(r.InventoryCountsDraft!.Count, "inventaires physiques en brouillon");

            case "po-approve":
                return Count(r.PurchaseOrdersDraft!.Count, "bons de commande en brouillon");

            case "po-receive":
                // CanReceive est un drapeau serveur : c'est lui qui est compté, pas le statut.
                return Count(r.PurchaseOrdersApproved!.Count(order => order.CanReceive), "commandes approuvées, marchandises attendues");

            case "haccp":
                return Count(r.HaccpReadings!.Count, "relevés de température non conformes aujourd'hui");

            case "absences":
                return Count(r.AbsencesRequested!.Count, "demandes d'absence en attente de décision");

            case "payroll":
            {
                var period = r.PayrollPeriods!.FirstOrDefault(candidate => candidate.Status != PayrollPeriodStatus.Closed);
                return period is null
                    ? Count(0, "aucune période de paie ouverte")
                    : Count(period.DraftPayslipCount, $"{period.Period} · {period.PayslipCount} bulletins");
            }

            case "events-today":
            {
                var events = r.EventsToday!;
                var first = events.FirstOrDefault();
                return Count(events.Count, first is null ? "aucun événement aujourd'hui" : $"{first.Title} · {first.FunctionSpaceLabel}");
            }

            // ------------------------------------------------------------ À surveiller
            case "hk-ooo":
                return Count(r.HousekeepingBoard!.OutOfOrderRooms, "chambres hors service ou hors vente");

            case "low-stock":
                return Count(r.LowStock!.Count, "articles sous leur seuil minimum");

            case "workstations":
            {
                var registry = r.Workstations!;
                var silent = registry.Workstations.Count(station => !string.Equals(station.Freshness, "Recent", StringComparison.OrdinalIgnoreCase));
                return Count(registry.Workstations.Count, $"{registry.DistinctAppVersions} versions · {silent} sans contact récent");
            }

            default:
                throw new KeyNotFoundException($"Aucune projection pour la file '{id}'.");
        }
    }
}
