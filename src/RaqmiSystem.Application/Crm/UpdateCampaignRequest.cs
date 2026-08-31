using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record UpdateCampaignRequest(
    string Label,
    CampaignChannel Channel,
    DateOnly StartDate,
    DateOnly EndDate,
    string? TargetSegmentCode = null,
    string? Objective = null,
    string? Message = null);
