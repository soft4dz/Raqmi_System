namespace RaqmiSystem.Application.Approvals;

/// <summary>
/// One step of a circuit as submitted by a client. No rank: ranks are assigned by the domain,
/// contiguous from 1, in the order the steps are provided.
/// </summary>
public sealed record ApprovalStepRequest(
    string Label,
    string RequiredRole);
