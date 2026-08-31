using RaqmiSystem.Application.Common;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

/// <summary>
/// The one question a consuming module asks the approvals module: "may this subject proceed?".
///
/// Backward-compatible by design: when NO active circuit covers the subject type, the answer is
/// true - installations that never configured a circuit keep working exactly as before. When an
/// active circuit exists, the answer is true only if an APPROVED instance exists for the
/// reference; anything else (no instance, in progress, rejected) is false.
///
/// Intended wiring (integrator): TreasuryService.ApprovePaymentOrderAsync calls
/// IsApprovedAsync(ApprovalSubjectType.PaymentOrder, id.ToString(), ...) and refuses explicitly
/// when the gate answers false.
/// </summary>
public interface IApprovalGate
{
    Task<ApplicationResult<bool>> IsApprovedAsync(
        ApprovalSubjectType type,
        string reference,
        CancellationToken cancellationToken);
}
