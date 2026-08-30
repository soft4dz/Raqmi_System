using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Identity;

/// <summary>
/// User administration (module "Administration et utilisateurs").
///
/// Every mutation takes the caller's <see cref="OperationContext"/>, and not only to stamp the
/// audit trail: the anti-lockout guards this module is built around are expressed in terms of
/// WHO is acting. They are enforced here, in the service, never in the UI - a screen that hides a
/// button does not stop an HTTP client:
///
/// <list type="number">
///   <item>a user cannot deactivate their own account;</item>
///   <item>a user cannot strip themselves of the role that grants users.write;</item>
///   <item>the last ACTIVE holder of users.write can be neither deactivated nor stripped of it,
///         by anyone - the installation must always keep at least one account able to administer
///         users, otherwise it becomes permanently unadministrable.</item>
/// </list>
/// </summary>
public interface IUserAdministrationService
{
    Task<IReadOnlyCollection<UserAccountResponse>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken);

    Task<ApplicationResult<UserAccountDetailResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an account with a server-generated temporary password, returned once in
    /// <see cref="CreateUserResponse.TemporaryPassword"/>.
    /// </summary>
    Task<ApplicationResult<CreateUserResponse>> CreateAsync(
        CreateUserRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<UserAccountDetailResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<UserAccountDetailResponse>> SetActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<UserAccountDetailResponse>> SetRolesAsync(
        Guid id,
        IReadOnlyCollection<string> roleNames,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lifts an ongoing failed-login lockout immediately, instead of leaving the owner locked out
    /// for the remainder of the policy window.
    /// </summary>
    Task<ApplicationResult<UserAccountDetailResponse>> UnlockAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);
}
