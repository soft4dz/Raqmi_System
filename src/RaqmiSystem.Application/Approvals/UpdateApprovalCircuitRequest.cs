namespace RaqmiSystem.Application.Approvals;

/// <summary>
/// Full replacement of a circuit's label and ordered steps. The subject type is deliberately
/// absent: it is fixed at creation (see ApprovalCircuit.UpdateDetails).
/// </summary>
public sealed record UpdateApprovalCircuitRequest(
    string Label,
    IReadOnlyCollection<ApprovalStepRequest> Steps);
