namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Outcome of a successful self-service password change. It quotes no password, neither the old one
/// nor the new one.
///
/// <see cref="MustChangePassword"/> is always false here - it is the flag the change exists to lift -
/// and is returned explicitly so a client that gated its screens on the same field of
/// <see cref="AuthenticatedUser"/> can clear that state without waiting for the next sign-in.
///
/// <see cref="RevokedSessionCount"/> tells the owner how many refresh tokens the change destroyed,
/// i.e. how many sessions were signed out. It is the visible confirmation that changing a password
/// after a suspected compromise actually ejected whoever else was holding a session. It counts every
/// token that was not already revoked, which in practice is the live sessions plus any abandoned
/// token that expired without ever being rotated - the revoking UPDATE cannot filter on expiry (see
/// the comment on it in AccountService.ChangePasswordAsync), so this number is an upper bound on the
/// sessions that were genuinely still usable, never an under-count.
/// </summary>
public sealed record ChangePasswordResponse(
    Guid UserId,
    string UserName,
    bool MustChangePassword,
    int RevokedSessionCount,
    DateTimeOffset ChangedAt);
