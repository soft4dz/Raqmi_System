using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// One approval in flight (or finished) for one business subject. The circuit's steps are
/// SNAPSHOTTED at opening time (<see cref="ApprovalInstanceStep"/>): a circuit modified or
/// deactivated afterwards never changes an instance already opened - the same immutability
/// doctrine as the customer/issuer snapshots frozen onto issued invoices.
///
/// Every workflow invariant lives here, in the entity:
///  - steps are decided strictly IN ORDER (callers never pick a step, the instance knows its
///    current one),
///  - the decider must carry the system role the current step requires,
///  - a rejection CLOSES the instance and demands a comment,
///  - an approved or rejected instance is immutable (services add an atomic status guard on
///    top, AccountingService-style, so concurrency cannot bypass this rule either).
/// </summary>
public sealed class ApprovalInstance : AuditableEntity
{
    private readonly List<ApprovalInstanceStep> _steps = new();

    private readonly List<ApprovalDecision> _decisions = new();

    private ApprovalInstance()
    {
    }

    public ApprovalInstance(ApprovalCircuit circuit, string subjectReference)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        if (!circuit.IsActive)
        {
            throw new InvalidOperationException("An approval instance can only be opened on an active circuit.");
        }

        if (circuit.Steps.Count == 0)
        {
            throw new InvalidOperationException("An approval instance requires a circuit with at least one step.");
        }

        SubjectType = circuit.SubjectType;
        SubjectReference = RequireValue(subjectReference, nameof(subjectReference), 100);
        CircuitCode = circuit.Code;
        CircuitLabel = circuit.Label;
        Status = ApprovalInstanceStatus.InProgress;

        // The snapshot: copies, not references. From this point on the circuit may change or
        // disappear, this instance keeps demanding exactly what was configured when it opened.
        foreach (var step in circuit.Steps.OrderBy(step => step.Rank))
        {
            _steps.Add(new ApprovalInstanceStep(step.Rank, step.Label, step.RequiredRole));
        }
    }

    public ApprovalSubjectType SubjectType { get; private set; } = ApprovalSubjectType.PaymentOrder;

    /// <summary>
    /// Identifier of the subject in its own module, carried as an opaque string (a payment
    /// order's Guid, later maybe an invoice number...). The approvals module never dereferences
    /// it - consumers ask the gate with the same reference they opened the instance with.
    /// </summary>
    public string SubjectReference { get; private set; } = string.Empty;

    /// <summary>Code of the circuit at opening time (snapshot: the circuit may be renamed or deleted later).</summary>
    public string CircuitCode { get; private set; } = string.Empty;

    public string CircuitLabel { get; private set; } = string.Empty;

    public ApprovalInstanceStatus Status { get; private set; } = ApprovalInstanceStatus.InProgress;

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public IReadOnlyCollection<ApprovalInstanceStep> Steps => _steps.AsReadOnly();

    public IReadOnlyCollection<ApprovalDecision> Decisions => _decisions.AsReadOnly();

    /// <summary>
    /// The step awaiting a decision, or null once the instance is closed. Steps are decided in
    /// order by construction: the current step is simply the lowest rank without a decision.
    /// </summary>
    public ApprovalInstanceStep? CurrentStep
    {
        get
        {
            if (Status != ApprovalInstanceStatus.InProgress)
            {
                return null;
            }

            var decidedRanks = _decisions.Select(decision => decision.Rank).ToHashSet();

            return _steps
                .OrderBy(step => step.Rank)
                .FirstOrDefault(step => !decidedRanks.Contains(step.Rank));
        }
    }

    /// <summary>
    /// Records the decision on the CURRENT step - callers never choose a step, which is what
    /// enforces the in-order rule. <paramref name="deciderRoles"/> are the system roles the
    /// decider carries (from the authenticated principal's role claims).
    /// </summary>
    public void Decide(
        string deciderUserName,
        IReadOnlyCollection<string> deciderRoles,
        bool approved,
        string? comment,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(deciderRoles);

        if (Status != ApprovalInstanceStatus.InProgress)
        {
            throw new InvalidOperationException(
                "This approval instance is closed: an approved or rejected instance is immutable.");
        }

        var currentStep = CurrentStep
            ?? throw new InvalidOperationException("This approval instance has no pending step left.");

        if (!deciderRoles.Contains(currentStep.RequiredRole, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Deciding step {currentStep.Rank} ({currentStep.Label}) requires the '{currentStep.RequiredRole}' role.");
        }

        // The decision's own constructor enforces the mandatory-comment-on-rejection rule.
        _decisions.Add(new ApprovalDecision(
            currentStep.Rank,
            currentStep.Label,
            deciderUserName,
            approved,
            comment,
            utcNow));

        if (!approved)
        {
            // One rejection closes the whole instance: the remaining steps become moot.
            Close(ApprovalInstanceStatus.Rejected, deciderUserName, utcNow);
            return;
        }

        var lastRank = _steps.Max(step => step.Rank);

        if (currentStep.Rank == lastRank)
        {
            Close(ApprovalInstanceStatus.Approved, deciderUserName, utcNow);
        }
    }

    private void Close(ApprovalInstanceStatus finalStatus, string userName, DateTimeOffset utcNow)
    {
        Status = finalStatus;
        ClosedAt = utcNow;
        ClosedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
