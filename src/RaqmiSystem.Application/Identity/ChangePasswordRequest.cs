namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Self-service password change payload.
///
/// It carries NO user identifier, and that absence is the security property of this module: the
/// account acted upon is the one the bearer token authenticates, read server-side from the token's
/// claims. Were the caller allowed to name a target, any authenticated user - the lowest-privilege
/// reader included - could set anyone else's password by simply quoting their identifier, which is
/// exactly the escalation the users.write-protected administrative reset exists to gate.
///
/// <see cref="CurrentPassword"/> is required even though the caller is already authenticated: a
/// stolen or borrowed session must not be enough to lock the legitimate owner out of their own
/// account.
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
