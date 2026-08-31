namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// The kinds of business documents an approval circuit can govern. Deliberately open-ended:
/// today the only real consumer is the treasury payment order, but the whole approvals module
/// (circuits, instances, snapshots, the <c>IApprovalGate</c> contract) is written against this
/// enum so that plugging a new subject (a budget, an invoice cancellation, a purchase request...)
/// is one new member here plus one gate call in the consuming service - nothing else.
/// </summary>
public enum ApprovalSubjectType
{
    PaymentOrder = 1
}
