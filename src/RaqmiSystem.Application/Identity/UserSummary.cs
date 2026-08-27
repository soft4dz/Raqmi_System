namespace RaqmiSystem.Application.Identity;

public sealed record UserSummary(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword);
