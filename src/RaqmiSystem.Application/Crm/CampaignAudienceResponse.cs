using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

/// <summary>
/// Who a campaign actually reaches, and who it does not. <paramref name="ExcludedForConsent"/> is
/// the number of guests of the target segment a direct channel must leave out for want of a
/// recorded opt-in: showing it is what stops the audience count from silently looking like a
/// targeting mistake.
///
/// <paramref name="ExcludedForMissingContact"/> counts the guests the channel cannot physically
/// reach - no email address for an email campaign, no phone number for an SMS or a call.
/// </summary>
public sealed record CampaignAudienceResponse(
    string CampaignCode,
    CampaignChannel Channel,
    bool RequiresMarketingConsent,
    string? TargetSegmentCode,
    int Reachable,
    int ExcludedForConsent,
    int ExcludedForMissingContact,
    IReadOnlyCollection<CampaignAudienceMember> Members);
