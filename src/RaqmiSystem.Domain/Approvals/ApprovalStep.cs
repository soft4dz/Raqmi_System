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
    /// Single source of truth for the roles a step may require: the REAL system roles of
    /// <see cref="RoleCatalog"/>, nothing invented. Exposed so that the desktop client offers
    /// exactly this list instead of restating it, and so a role later added to the catalog only
    /// needs to be added here to become eligible.
    /// </summary>
    public static readonly IReadOnlyCollection<string> AllowedRoles = new[]
    {
        RoleCatalog.SystemAdministrator,
        RoleCatalog.Direction,
        RoleCatalog.ExploitationControl,
        RoleCatalog.UnitManager,
        RoleCatalog.Cashier,
        RoleCatalog.Reader
    };

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
    /// Validates that a required role is one of the real system roles. Exposed because the
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
                $"Required role must be one of the system roles: {string.Join(", ", AllowedRoles)}.",
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
