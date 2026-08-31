namespace RaqmiSystem.Application.Approvals;

public sealed record ApprovalDecisionResponse(
    int Rank,
    string StepLabel,
    string DecidedBy,
    bool Approved,
    string? Comment,
    DateTimeOffset DecidedAt);
