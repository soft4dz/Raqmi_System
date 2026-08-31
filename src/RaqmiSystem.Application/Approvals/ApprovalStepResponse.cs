namespace RaqmiSystem.Application.Approvals;

public sealed record ApprovalStepResponse(
    int Rank,
    string Label,
    string RequiredRole);
