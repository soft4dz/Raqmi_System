using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

/// <summary>
/// Opens an approval instance for one business subject. The reference is the subject's
/// identifier in its own module (for a payment order: its Guid id as a string) - the same
/// string consumers later hand to IApprovalGate.IsApprovedAsync.
/// </summary>
public sealed record OpenApprovalInstanceRequest(
    ApprovalSubjectType SubjectType,
    string SubjectReference);
