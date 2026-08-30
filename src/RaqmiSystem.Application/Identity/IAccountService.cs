using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Identity;

/// <summary>
/// What an authenticated user may do to their OWN account, as opposed to
/// <see cref="IUserAdministrationService"/>, which is what an administrator may do to someone
/// else's.
///
/// It exists because the two halves of the password lifecycle were not symmetric: account creation
/// and the administrative reset both hand out a server-generated temporary password and raise
/// <c>User.MustChangePassword</c>, but nothing could ever lower that flag again. The account was
/// left indefinitely on a password its creator knows by construction, and the flag - the one signal
/// telling a client "make this person choose a password" - could only ever be observed, never
/// satisfied. This service closes that loop.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Replaces the caller's own password.
    /// </summary>
    /// <param name="userId">
    /// The account to act on. It is the caller's own identity, taken from the authenticated token by
    /// the endpoint - never a value supplied in the request body, which would turn a change-my-own
    /// -password route into an unauthenticated-target password overwrite.
    /// </param>
    /// <param name="currentPassword">
    /// Re-authentication of the person behind the session. A wrong one is rejected with the same
    /// message as an account that no longer exists: the caller already holds a token for this
    /// account, so the two cases must not be distinguishable from the outside.
    /// </param>
    /// <param name="newPassword">
    /// Must satisfy <see cref="PasswordPolicy"/> and differ from <paramref name="currentPassword"/>.
    /// </param>
    /// <remarks>
    /// A successful change also revokes every still-active refresh token of the account. That is the
    /// behaviour that makes a password change worth anything after a compromise: without it the
    /// intruder's session simply outlives the change, silently renewing itself for the remainder of
    /// the refresh-token lifetime. The caller's own other sessions go down with it, which is the
    /// universally expected trade-off of "sign me out everywhere".
    /// </remarks>
    Task<ApplicationResult<ChangePasswordResponse>> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        OperationContext context,
        CancellationToken cancellationToken);
}
