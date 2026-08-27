namespace RaqmiSystem.Application.Identity;

public sealed record PermissionSummary(
    string Key,
    string Name,
    string Category,
    string Description);
