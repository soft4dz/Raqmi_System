namespace RaqmiSystem.Application.Identity;

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
