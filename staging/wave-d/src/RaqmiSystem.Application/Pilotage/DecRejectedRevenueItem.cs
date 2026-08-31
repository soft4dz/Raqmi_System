namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One REJECTED daily revenue entry awaiting correction by its unit. Verified mechanic
/// (see DailyRevenue): a rejected entry keeps Status == Rejected with its RejectionReason
/// until someone edits it - UpdateAmounts then returns it to Draft AND clears the reason.
/// A "Draft with a rejection reason" state therefore never exists; the queue is exactly
/// the entries at the Rejected status.
/// </summary>
public sealed record DecRejectedRevenueItem(
    Guid Id,
    string HotelUnitCode,
    string? HotelUnitName,
    DateOnly BusinessDate,
    decimal Total,
    string? RejectionReason,
    DateTimeOffset? RejectedAt,
    int AgeDays);
