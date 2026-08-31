using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Application.Approvals;

public interface IApprovalService
{
    Task<IReadOnlyCollection<ApprovalCircuitResponse>> ListCircuitsAsync(
        ApprovalSubjectType? subjectType,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalCircuitResponse>> GetCircuitAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalCircuitResponse>> CreateCircuitAsync(
        CreateApprovalCircuitRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalCircuitResponse>> UpdateCircuitAsync(
        string code,
        UpdateApprovalCircuitRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalCircuitResponse>> SetCircuitActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>History and monitoring: instances filtered by subject, reference, status and opening period.</summary>
    Task<IReadOnlyCollection<ApprovalInstanceResponse>> ListInstancesAsync(
        ApprovalSubjectType? subjectType,
        string? subjectReference,
        ApprovalInstanceStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalInstanceResponse>> GetInstanceAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// "My pending decisions": in-progress instances whose CURRENT step requires one of the
    /// caller's roles.
    /// </summary>
    Task<IReadOnlyCollection<ApprovalInstanceResponse>> ListPendingAsync(
        IReadOnlyCollection<string> deciderRoles,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ApprovalInstanceResponse>> OpenInstanceAsync(
        OpenApprovalInstanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Decides the current step of an instance. <paramref name="deciderRoles"/> are the
    /// authenticated caller's role claims: the domain matches them against the role the current
    /// step requires.
    /// </summary>
    Task<ApplicationResult<ApprovalInstanceResponse>> DecideAsync(
        Guid id,
        bool approved,
        string? comment,
        IReadOnlyCollection<string> deciderRoles,
        OperationContext context,
        CancellationToken cancellationToken);
}
