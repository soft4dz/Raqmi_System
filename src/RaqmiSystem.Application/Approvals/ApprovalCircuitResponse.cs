using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

public sealed record ApprovalCircuitResponse(
    Guid Id,
    string Code,
    string Label,
    ApprovalSubjectType SubjectType,
    bool IsActive,
    IReadOnlyCollection<ApprovalStepResponse> Steps,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
