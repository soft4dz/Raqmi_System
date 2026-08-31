namespace RaqmiSystem.Application.Crm;

/// <summary>One guest the campaign reaches, with the contact details its channel needs.</summary>
public sealed record CampaignAudienceMember(
    string CustomerCode,
    string CustomerName,
    string? SegmentCode,
    string? Email,
    string? Phone,
    bool MarketingConsent,
    bool IsVip,
    int LoyaltyPoints);
