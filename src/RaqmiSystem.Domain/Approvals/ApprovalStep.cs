using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// One ordered step of an approval circuit, modelled as a child entity with its own table and a
/// required FK to <see cref="ApprovalCircuit"/> (same pattern as <c>InvoiceLine</c>): a dedicated
/// entity keeps the snake_case table configuration, named indexes and check constraints explicit,
/// and lets steps carry a stable Id referenceable from API responses.
/// </summary>
public sealed class ApprovalStep
{
    /// <summary>
    /// The roles a step may require: exactly <see cref="RoleCatalog.ApprovalDeciderRoles"/>, the
    /// roles that hold approvals.decide - never every role of the catalog. A step demanding a
    /// role that cannot decide (cashier, reader) would be undecidable for life, and the snapshot
    /// taken when an instance opens would freeze that dead end into every open instance; the
    /// refusal therefore happens here, at creation time, where it is still recoverable.
    ///
    /// The list is not restated: it is the shared constant of the identity catalog, so the API,
    /// the desktop client (which fills its role picker from here) and the seeder all read the
    /// same truth.
    /// </summary>
    public static IReadOnlyCollection<string> AllowedRoles => RoleCatalog.ApprovalDeciderRoles;

    private ApprovalStep()
    {
    }

    public ApprovalStep(string label, string requiredRole)
    {
        Label = RequireValue(label, nameof(label), 200);
        RequiredRole = RequireAllowedRole(requiredRole, nameof(requiredRole));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CircuitId { get; private set; }

    /// <summary>Position of the step in the circuit, contiguous from 1 (assigned by
    /// <see cref="ApprovalCircuit.ReplaceSteps"/>, never by callers).</summary>
    public int Rank { get; private set; }

    public string Label { get; private set; } = string.Empty;

    /// <summary>Name of the <see cref="RoleCatalog"/> role a decider must carry for this step.</summary>
    public string RequiredRole { get; private set; } = string.Empty;

    internal void SetRank(int rank)
    {
        Rank = rank;
    }

    /// <summary>
    /// Validates that a required role is one of the roles able to DECIDE. Exposed because the
    /// instance snapshot (<see cref="ApprovalInstanceStep"/>) is held to the same rule.
    /// </summary>
    public static string RequireAllowedRole(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Required role is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (!AllowedRoles.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Required role must be one of the roles that can decide an approval " +
                $"(they alone hold {PermissionCatalog.ApprovalsDecide}): {string.Join(", ", AllowedRoles)}.",
                argumentName);
        }

        // The canonical casing of the catalog is stored, whatever the caller typed: role
        // matching at decision time must never depend on how the circuit was captured.
        return AllowedRoles.First(role => string.Equals(role, trimmed, StringComparison.OrdinalIgnoreCase));
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
