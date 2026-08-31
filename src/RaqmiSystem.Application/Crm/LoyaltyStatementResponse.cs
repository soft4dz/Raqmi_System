namespace RaqmiSystem.Application.Crm;

/// <summary>
/// The loyalty position of one guest: the balance, the tier it reaches, what is left to reach the
/// next one, and the movements that justify the balance. Everything but the movements is derived,
/// so the statement can never claim a tier the ledger below it does not support.
/// </summary>
public sealed record LoyaltyStatementResponse(
    string CustomerCode,
    string CustomerName,
    int Balance,
    string? TierCode,
    string? TierLabel,
    string? TierBenefits,
    string? NextTierCode,
    string? NextTierLabel,
    int? PointsToNextTier,
    IReadOnlyCollection<LoyaltyTransactionResponse> Movements);
