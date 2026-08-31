using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// A configurable validation circuit: an ordered list of steps, each requiring a system role,
/// applied to every subject of its <see cref="SubjectType"/> while the circuit is active.
///
/// A circuit is only a TEMPLATE: opening an <see cref="ApprovalInstance"/> snapshots the steps
/// as they stand at that moment (same immutability doctrine as issued-invoice snapshots), so a
/// circuit can be freely edited or deactivated without rewriting in-flight approvals.
/// </summary>
public sealed class ApprovalCircuit : AuditableEntity
{
    private readonly List<ApprovalStep> _steps = new();

    private ApprovalCircuit()
    {
    }

    public ApprovalCircuit(string code, string label, ApprovalSubjectType subjectType)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 200);
        SubjectType = RequireDefined(subjectType);

        // A circuit is born inactive: activation is a distinct, deliberate act, gated on the
        // "at least one step" invariant. This mirrors the draft-then-issue lifecycle of the
        // other engaging documents of the system.
        IsActive = false;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public ApprovalSubjectType SubjectType { get; private set; } = ApprovalSubjectType.PaymentOrder;

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ApprovalStep> Steps => _steps.AsReadOnly();

    /// <summary>
    /// Replaces the ordered steps. Ranks are assigned here, contiguous from 1, in the order the
    /// caller provides - callers never number steps themselves, so gaps and duplicates cannot
    /// exist by construction. An ACTIVE circuit may be re-ordered or re-worded but never left
    /// without steps: an active circuit with zero steps would block every subject it covers with
    /// no one able to unblock them.
    /// </summary>
    public void ReplaceSteps(IReadOnlyCollection<ApprovalStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (IsActive && steps.Count == 0)
        {
            throw new InvalidOperationException("An active approval circuit must keep at least one step.");
        }

        _steps.Clear();

        var rank = 1;

        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step);
            step.SetRank(rank++);
            _steps.Add(step);
        }
    }

    public void UpdateDetails(string label)
    {
        // The subject type is deliberately NOT editable: a circuit that changes subject mid-life
        // would silently re-aim past audits and open instances. Retire the circuit and create a
        // new one instead.
        Label = RequireValue(label, nameof(label), 200);
    }

    public void Activate()
    {
        // The founding invariant: a circuit without steps cannot demand anything, so activating
        // it would only ever block subjects (the gate refuses anything not approved) with no
        // step anyone could decide.
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("An approval circuit requires at least one step to be activated.");
        }

        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static ApprovalSubjectType RequireDefined(ApprovalSubjectType subjectType)
    {
        if (!Enum.IsDefined(subjectType))
        {
            throw new ArgumentOutOfRangeException(nameof(subjectType), subjectType, "Unknown approval subject type.");
        }

        return subjectType;
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
