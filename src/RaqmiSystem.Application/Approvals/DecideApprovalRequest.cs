namespace RaqmiSystem.Application.Approvals;

/// <summary>
/// Body of an approve/reject call. The comment is optional on an approval and MANDATORY on a
/// rejection (enforced by the domain).
/// </summary>
public sealed record DecideApprovalRequest(string? Comment);
