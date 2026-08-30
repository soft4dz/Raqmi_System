namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Creation payload. There is deliberately no password field: an administrator never chooses
/// another person's password. The server generates a temporary one and returns it exactly once
/// (see <see cref="CreateUserResponse"/>).
/// </summary>
public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string>? Roles = null);
