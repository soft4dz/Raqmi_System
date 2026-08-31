namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Shared string guards for the HR entities. The module handles a lot of short identifying
/// values (employee numbers, national identity numbers, bank accounts) whose length limits are
/// also the column widths, so the check lives in one place instead of being retyped - and
/// slightly mistyped - in every entity.
/// </summary>
internal static class HumanResourcesText
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
}
