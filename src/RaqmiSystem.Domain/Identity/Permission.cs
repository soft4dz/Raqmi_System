using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Identity;

public sealed class Permission : AuditableEntity
{
    private Permission()
    {
    }

    public Permission(string key, string name, string category, string description)
    {
        Key = RequireValue(key, nameof(key)).ToLowerInvariant();
        Name = RequireValue(name, nameof(name));
        Category = RequireValue(category, nameof(category)).ToLowerInvariant();
        Description = RequireValue(description, nameof(description));
        IsSystem = true;
    }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
