namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// Shared string guards for the CRM entities. The module carries the same handful of short
/// identifying values (codes, labels) and a lot of free text (preferences, comments, campaign
/// messages) whose length limits are also the column widths, so the check lives in one place
/// instead of being retyped - and slightly mistyped - in every entity.
/// </summary>
internal static class CrmText
{
    public static string Require(string value, string argumentName, int maxLength)
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

    /// <summary>
    /// Optional counterpart of <see cref="Require"/>: blank collapses to null so the database
    /// never holds an empty string that means the same thing as a missing value.
    /// </summary>
    public static string? Optional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Require(value, argumentName, maxLength);
    }

    /// <summary>
    /// Normalizes a business code the way the rest of the ERP does: required, trimmed, upper
    /// case. Codes are compared and joined on across modules, so their case must not be a
    /// property of how someone typed them.
    /// </summary>
    public static string RequireCode(string value, string argumentName, int maxLength = 40)
    {
        return Require(value, argumentName, maxLength).ToUpperInvariant();
    }

    /// <summary>Optional counterpart of <see cref="RequireCode"/>.</summary>
    public static string? OptionalCode(string? value, string argumentName, int maxLength = 40)
    {
        return Optional(value, argumentName, maxLength)?.ToUpperInvariant();
    }
}
