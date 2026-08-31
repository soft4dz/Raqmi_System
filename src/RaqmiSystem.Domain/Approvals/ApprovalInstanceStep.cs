namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// The SNAPSHOT of one circuit step, frozen into an <see cref="ApprovalInstance"/> at the moment
/// it is opened. Copying the steps (rather than referencing the live circuit) is what makes an
/// in-flight approval immune to later circuit edits - the same legal-immutability doctrine as
/// the customer/issuer snapshots on issued invoices.
/// </summary>
public sealed class ApprovalInstanceStep
{
    private ApprovalInstanceStep()
    {
    }

    public ApprovalInstanceStep(int rank, string label, string requiredRole)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be at least 1.");
        }

        Rank = rank;
        Label = RequireValue(label, nameof(label), 200);
        RequiredRole = ApprovalStep.RequireAllowedRole(requiredRole, nameof(requiredRole));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InstanceId { get; private set; }

    public int Rank { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string RequiredRole { get; private set; } = string.Empty;

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
