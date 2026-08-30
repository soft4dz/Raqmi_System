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

    /// <summary>
    /// Aligns the display fields (name, category, description) on the catalog definition. The
    /// KEY is the permission's identity and never changes - only the human-readable labels
    /// follow the catalog, so a wording fixed in <c>PermissionCatalog</c> reaches databases
    /// seeded before the fix. Returns true when something actually changed (idempotent
    /// otherwise), so the caller can stamp the update only when one happened.
    /// </summary>
    public bool SyncDefinition(string name, string category, string description)
    {
        var normalizedName = RequireValue(name, nameof(name));
        var normalizedCategory = RequireValue(category, nameof(category)).ToLowerInvariant();
        var normalizedDescription = RequireValue(description, nameof(description));

        if (Name == normalizedName && Category == normalizedCategory && Description == normalizedDescription)
        {
            return false;
        }

        Name = normalizedName;
        Category = normalizedCategory;
        Description = normalizedDescription;
        return true;
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
