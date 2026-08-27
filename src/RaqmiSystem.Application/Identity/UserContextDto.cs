namespace RaqmiSystem.Application.Identity;

public sealed record UserContextDto(
    string UserName,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
