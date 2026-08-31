namespace RaqmiSystem.Application.Crm;

/// <summary>
/// A guest profile joined to the customer file it extends and to the loyalty programme. The
/// loyalty balance and tier are derived from the point ledger at read time - the profile row
/// itself holds neither.
/// </summary>
public sealed record GuestProfileResponse(
    Guid Id,
    string CustomerCode,
    string CustomerName,
    string? SegmentCode,
    string? SegmentLabel,
    string? PreferredLanguage,
    DateOnly? BirthDate,
    string? Preferences,
    string? Notes,
    bool IsVip,
    bool MarketingConsent,
    DateTimeOffset? MarketingConsentUpdatedAt,
    int LoyaltyPoints,
    string? LoyaltyTierCode,
    string? LoyaltyTierLabel,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
