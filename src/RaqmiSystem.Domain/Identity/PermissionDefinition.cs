namespace RaqmiSystem.Domain.Identity;

public sealed record PermissionDefinition(
    string Key,
    string Name,
    string Category,
    string Description);
