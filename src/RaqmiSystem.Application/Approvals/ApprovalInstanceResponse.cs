using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

public sealed record ApprovalInstanceResponse(
    Guid Id,
    ApprovalSubjectType SubjectType,
    string SubjectReference,
    string CircuitCode,
    string CircuitLabel,
    ApprovalInstanceStatus Status,
    int? CurrentRank,
    string? CurrentStepLabel,
    string? CurrentStepRequiredRole,
    IReadOnlyCollection<ApprovalInstanceStepResponse> Steps,
    IReadOnlyCollection<ApprovalDecisionResponse> Decisions,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
