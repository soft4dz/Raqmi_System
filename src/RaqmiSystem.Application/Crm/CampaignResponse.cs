using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record CampaignResponse(
    Guid Id,
    string Code,
    string Label,
    CampaignChannel Channel,
    string? TargetSegmentCode,
    string? TargetSegmentLabel,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus Status,
    string? Objective,
    string? Message,
    bool CanEdit,
    bool RequiresMarketingConsent,
    DateTimeOffset? ScheduledAt,
    string? ScheduledBy,
    DateTimeOffset? LaunchedAt,
    string? LaunchedBy,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
