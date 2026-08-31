namespace RaqmiSystem.Application.Crm;

/// <summary>
/// One movement to post on a guest's point ledger. The KIND is carried by the operation being
/// called (earn, redeem, expire, adjust), not by this record: it is what decides the sign of the
/// movement, so leaving it to the payload would let a redemption ask to be credited.
///
/// <paramref name="Points"/> is a strictly positive quantity of points for earn, redeem and
/// expire - the service applies the sign. For an adjustment, and only there, it is a signed
/// correction, because a correction is the one movement that genuinely goes either way.
/// </summary>
public sealed record LoyaltyMovementRequest(
    int Points,
    DateOnly OccurredOn,
    string Reason,
    string? Reference = null);
