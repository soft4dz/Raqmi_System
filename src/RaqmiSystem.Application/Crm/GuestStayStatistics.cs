namespace RaqmiSystem.Application.Crm;

/// <summary>
/// What the module that keeps the stays knows about a guest, summed up. Read at query time
/// through <see cref="IStayHistoryReader"/> and never stored: the CRM must not keep its own
/// copy of a stay history the front desk keeps changing.
///
/// <paramref name="StayRevenue"/> counts only the stays that HAPPENED (checked in or checked
/// out): a booking still to come is not revenue, and a cancellation never was.
/// </summary>
public sealed record GuestStayStatistics(
    int StayCount,
    int NightCount,
    DateOnly? FirstArrival,
    DateOnly? LastDeparture,
    decimal StayRevenue,
    int UpcomingCount,
    int CancelledCount,
    int NoShowCount)
{
    /// <summary>
    /// Un client sans aucun séjour - ou une installation sans module de séjours. Les deux se
    /// lisent pareil dans la vue 360 : rien à montrer, et rien d'inventé.
    /// </summary>
    public static GuestStayStatistics Empty { get; } = new(0, 0, null, null, 0m, 0, 0, 0);
}
