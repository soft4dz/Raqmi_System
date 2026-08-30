namespace RaqmiSystem.Application.Identity;

/// <summary>
/// One row of the user administration list (GET /api/v1/security/users). On top of the plain
/// identity fields it carries the two states an administrator actually acts on: whether the
/// account is currently locked out by the failed-login policy, and which roles it holds.
/// </summary>
/// <param name="IsLockedOut">
/// Evaluated against the server clock when the response is built, so a lockout that has already
/// expired reads as false even though <paramref name="LockedOutUntil"/> is still populated.
/// </param>
public sealed record UserAccountResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    bool IsLockedOut,
    DateTimeOffset? LockedOutUntil,
    IReadOnlyCollection<string> Roles);
