namespace RaqmiSystem.Application.Crm;

/// <summary>
/// What the lodging module knows about a guest, summed up. Read from the reservations at query
/// time and never stored: the CRM must not keep its own copy of a stay history the front desk
/// keeps changing.
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
    int NoShowCount);
