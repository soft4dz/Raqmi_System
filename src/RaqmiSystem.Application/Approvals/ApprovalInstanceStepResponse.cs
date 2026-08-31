namespace RaqmiSystem.Application.Approvals;

public sealed record ApprovalInstanceStepResponse(
    int Rank,
    string Label,
    string RequiredRole,
    bool IsDecided);
