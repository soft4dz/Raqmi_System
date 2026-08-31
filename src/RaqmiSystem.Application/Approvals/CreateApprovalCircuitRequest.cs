using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

public sealed record CreateApprovalCircuitRequest(
    string Code,
    string Label,
    ApprovalSubjectType SubjectType,
    IReadOnlyCollection<ApprovalStepRequest> Steps);
