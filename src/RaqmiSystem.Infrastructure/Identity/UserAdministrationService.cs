using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Identity;

/// <summary>
/// Server side of the user administration module. See <see cref="IUserAdministrationService"/> for
/// the three anti-lockout guards; they are implemented here rather than in the endpoints so that
/// no caller - HTTP client, future desktop screen, or another service - can reach a mutation
/// without crossing them.
/// </summary>
public sealed class UserAdministrationService(
    RaqmiDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAuditLogWriter auditLogWriter) : IUserAdministrationService
{
    private const string AuditEntityName = "security.users";

    private const string SelfDeactivationRefused =
        "You cannot deactivate your own account. Ask another administrator to do it.";

    private const string SelfAdministrationRoleRemovalRefused =
        "You cannot remove your own user-administration role (users.write). Ask another administrator to do it.";

    private const string LastAdministratorRefused =
        "This is the last active user with the users.write permission: the installation must keep at least one, " +
        "otherwise no one could administer users any more. Grant users.write to another active account first.";

    private const string ConcurrentAdministrationRefused =
        "Another change on the same administrator accounts was committed at the same moment, so this one was " +
        "rolled back and nothing was modified. Reload the user list and try again.";

    public async Task<IReadOnlyCollection<UserAccountResponse>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Include(user => user.Roles)
            .ThenInclude(userRole => userRole.Role)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            // NormalizedUserName and NormalizedEmail are already upper-invariant (see User);
            // DisplayName is stored as typed, so it is the only one needing an upper() in SQL.
            query = query.Where(user =>
                user.NormalizedUserName.Contains(normalizedSearch) ||
                user.NormalizedEmail.Contains(normalizedSearch) ||
                user.DisplayName.ToUpper().Contains(normalizedSearch));
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .ToArrayAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return users.Select(user => Map(user, now)).ToArray();
    }

    public async Task<ApplicationResult<UserAccountDetailResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await WithRolesAndPermissions(dbContext.Users.AsNoTracking())
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        return user is null
            ? ApplicationResult<UserAccountDetailResponse>.NotFound("User was not found.")
            : ApplicationResult<UserAccountDetailResponse>.Success(MapDetail(user, DateTimeOffset.UtcNow));
    }

    public async Task<IReadOnlyCollection<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.IsActive)
            .OrderBy(role => role.DisplayName)
            .ToArrayAsync(cancellationToken);

        return roles
            .Select(role => new RoleSummary(role.Name, role.DisplayName, role.Description, role.IsSystem))
            .ToArray();
    }

    public async Task<ApplicationResult<CreateUserResponse>> CreateAsync(
        CreateUserRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = NormalizeOrEmpty(request.UserName);
        var normalizedEmail = NormalizeOrEmpty(request.Email);

        var takenUserName = await dbContext.Users
            .AnyAsync(current => current.NormalizedUserName == normalizedUserName, cancellationToken);

        if (takenUserName)
        {
            return ApplicationResult<CreateUserResponse>.Conflict("A user with this user name already exists.");
        }

        var takenEmail = await dbContext.Users
            .AnyAsync(current => current.NormalizedEmail == normalizedEmail, cancellationToken);

        if (takenEmail)
        {
            return ApplicationResult<CreateUserResponse>.Conflict("A user with this email already exists.");
        }

        var requestedRoles = await ResolveRolesAsync(request.Roles ?? [], cancellationToken);

        if (!requestedRoles.Succeeded)
        {
            return ApplicationResult<CreateUserResponse>.Validation(requestedRoles.Error!);
        }

        // The administrator never picks another person's password: it is generated here with a
        // CSPRNG, only its hash is persisted, it is never written to the audit log, and the
        // account is flagged MustChangePassword. Since this repository still has no email/SMTP
        // infrastructure, the response below is the only channel able to deliver it - exactly the
        // same trade-off as the existing reset-password endpoint.
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        User user;

        try
        {
            user = new User(
                request.UserName,
                request.Email,
                request.DisplayName,
                passwordHasher.Hash(temporaryPassword),
                mustChangePassword: true);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CreateUserResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;

        user.SetRoles(requestedRoles.Value!, now);
        user.MarkCreated(context.UserName, now);
        dbContext.Users.Add(user);

        try
        {
            await WriteAuditAsync(
                "security.user.created",
                user,
                context,
                new
                {
                    user.UserName,
                    user.Email,
                    user.DisplayName,
                    Roles = requestedRoles.Value!.Select(role => role.Name).Order().ToArray()
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The two availability checks above and this insert are not atomic: a concurrent
            // create with the same user name or email loses the race against the unique indexes
            // on normalized_user_name / normalized_email.
            return ApplicationResult<CreateUserResponse>.Conflict(
                "A user with this user name or email already exists.");
        }

        var detail = await LoadDetailAsync(user.Id, cancellationToken);

        return ApplicationResult<CreateUserResponse>.Success(new CreateUserResponse(detail, temporaryPassword));
    }

    public async Task<ApplicationResult<UserAccountDetailResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserAccountDetailResponse>.NotFound("User was not found.");
        }

        var previousEmail = user.Email;
        var previousDisplayName = user.DisplayName;

        try
        {
            // UserName is intentionally not part of the payload: it is the sign-in identifier.
            user.UpdateProfile(request.Email, request.DisplayName);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<UserAccountDetailResponse>.Validation(ex.Message);
        }

        user.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                "security.user.updated",
                user,
                context,
                new
                {
                    user.UserName,
                    Email = Describe(previousEmail, user.Email),
                    DisplayName = Describe(previousDisplayName, user.DisplayName)
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<UserAccountDetailResponse>.Conflict(
                "A user with this email already exists.");
        }

        return ApplicationResult<UserAccountDetailResponse>.Success(
            await LoadDetailAsync(user.Id, cancellationToken));
    }

    public async Task<ApplicationResult<UserAccountDetailResponse>> SetActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var user = await WithRolesAndPermissions(dbContext.Users)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserAccountDetailResponse>.NotFound("User was not found.");
        }

        if (!isActive)
        {
            // Guard (a). Checked before the last-administrator guard so that an administrator
            // deactivating themselves always gets the precise reason, whatever the population.
            if (context.UserId is not null && context.UserId == user.Id)
            {
                return ApplicationResult<UserAccountDetailResponse>.Validation(SelfDeactivationRefused);
            }

            // Guard (c). Only reachable for someone else's account here, and it really is
            // reachable: access tokens are permission snapshots, so a just-deactivated
            // administrator keeps a usable token until it expires and could otherwise close the
            // door behind them by deactivating the one administrator still standing.
            //
            // The "is there another active users.write holder?" question is deliberately NOT
            // answered here: asking it now and committing later is the very window two concurrent
            // deactivations slip through. It is asked and answered atomically, as part of the
            // statement that commits this deactivation - see RunGuardedMutationAsync.
            if (user.IsActive && GrantsUserAdministration(user))
            {
                return await RunGuardedMutationAsync(
                    user,
                    now =>
                    {
                        user.Deactivate();
                        user.MarkUpdated(context.UserName, now);

                        return WriteAuditAsync(
                            "security.user.deactivated",
                            user,
                            context,
                            new { user.UserName, user.IsActive },
                            cancellationToken);
                    },
                    cancellationToken);
            }
        }

        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        user.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "security.user.activated" : "security.user.deactivated",
            user,
            context,
            new { user.UserName, user.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<UserAccountDetailResponse>.Success(
            await LoadDetailAsync(user.Id, cancellationToken));
    }

    public async Task<ApplicationResult<UserAccountDetailResponse>> SetRolesAsync(
        Guid id,
        IReadOnlyCollection<string> roleNames,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var user = await WithRolesAndPermissions(dbContext.Users)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserAccountDetailResponse>.NotFound("User was not found.");
        }

        var requestedRoles = await ResolveRolesAsync(roleNames, cancellationToken);

        if (!requestedRoles.Succeeded)
        {
            return ApplicationResult<UserAccountDetailResponse>.Validation(requestedRoles.Error!);
        }

        var previousRoles = user.Roles.Select(userRole => userRole.Role.Name).Order().ToArray();
        var nextRoles = requestedRoles.Value!.Select(role => role.Name).Order().ToArray();

        var losesUserAdministration =
            GrantsUserAdministration(user) && !GrantsUserAdministration(requestedRoles.Value!);

        if (losesUserAdministration)
        {
            // Guard (b): the symmetric case of guard (a). Deactivating yourself and stripping
            // yourself of users.write both end with an administrator unable to get back in.
            if (context.UserId is not null && context.UserId == user.Id)
            {
                return ApplicationResult<UserAccountDetailResponse>.Validation(
                    SelfAdministrationRoleRemovalRefused);
            }

            // Guard (c), role-removal side: an active last administrator must keep the permission.
            // Same reasoning as on the deactivation side - and the two paths race against each
            // other just as well as against themselves, so both re-assert the invariant with the
            // same atomic claim.
            if (user.IsActive)
            {
                return await RunGuardedMutationAsync(
                    user,
                    now =>
                    {
                        user.SetRoles(requestedRoles.Value!, now);
                        user.MarkUpdated(context.UserName, now);

                        return WriteAuditAsync(
                            "security.user.roles_changed",
                            user,
                            context,
                            new { user.UserName, Before = previousRoles, After = nextRoles },
                            cancellationToken);
                    },
                    cancellationToken);
            }
        }

        user.SetRoles(requestedRoles.Value!, DateTimeOffset.UtcNow);
        user.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "security.user.roles_changed",
            user,
            context,
            new { user.UserName, Before = previousRoles, After = nextRoles },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<UserAccountDetailResponse>.Success(
            await LoadDetailAsync(user.Id, cancellationToken));
    }

    public async Task<ApplicationResult<UserAccountDetailResponse>> UnlockAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (user is null)
        {
            return ApplicationResult<UserAccountDetailResponse>.NotFound("User was not found.");
        }

        var now = DateTimeOffset.UtcNow;

        // Idempotent on purpose: unlocking an account that is not (or no longer) locked out is a
        // no-op rather than an error, so the screen never has to race the clock against an expiry.
        // The audit entry records which of the two it was.
        var wasLockedOut = user.IsLockedOut(now);

        user.Unlock();
        user.MarkUpdated(context.UserName, now);

        await WriteAuditAsync(
            "security.user.unlocked",
            user,
            context,
            new { user.UserName, WasLockedOut = wasLockedOut },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<UserAccountDetailResponse>.Success(
            await LoadDetailAsync(user.Id, cancellationToken));
    }

    /// <summary>
    /// Commits a mutation that takes <paramref name="user"/> out of the population of ACTIVE
    /// users.write holders - a deactivation or a demotion - and refuses it when that population
    /// would be emptied (guard (c)).
    ///
    /// Counting the other active holders with a SELECT and committing afterwards is not enough,
    /// however serializable the intent: two concurrent requests, each aimed at the other's target,
    /// both read "one other administrator is still active", both pass, both commit, and the
    /// installation is left with zero. So the count is not read here at all - it is re-asserted by
    /// <see cref="TryClaimAnotherActiveAdministratorAsync"/> as the WHERE clause of a single
    /// conditional UPDATE, the same claim-in-one-statement pattern
    /// AuthenticationService.RefreshAsync uses to make refresh-token rotation single-use. Only the
    /// request whose statement actually matched a row goes on to mutate, and claim, mutation and
    /// audit entry then commit (or roll back) as one.
    ///
    /// The transaction is Serializable because on PostgreSQL the conditional statement alone would
    /// not close the two-requests-on-two-different-rows case: under READ COMMITTED each statement
    /// reads a snapshot taken before the other transaction committed, so both EXISTS subqueries
    /// would still see the other administrator active. Serializable makes PostgreSQL abort one of
    /// the two (SQLSTATE 40001) rather than let both through; that abort is translated below into a
    /// plain retryable 409 instead of an escaping 500. SQLite - the integration-test provider,
    /// which does not implement snapshot isolation - serializes writers on its own, and there the
    /// conditional statement is what closes the window.
    /// </summary>
    /// <param name="user">The tracked user the mutation applies to.</param>
    /// <param name="mutateAndAuditAsync">
    /// Applies the mutation to <paramref name="user"/> and writes its audit entry, using the
    /// timestamp passed in. Called only once the claim succeeded, so a refused mutation never
    /// leaves a half-applied entity or an audit entry describing something that did not happen.
    /// </param>
    private async Task<ApplicationResult<UserAccountDetailResponse>> RunGuardedMutationAsync(
        User user,
        Func<DateTimeOffset, Task> mutateAndAuditAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimAnotherActiveAdministratorAsync(user.Id, now, cancellationToken))
            {
                return ApplicationResult<UserAccountDetailResponse>.Validation(LastAdministratorRefused);
            }

            await mutateAndAuditAsync(now);
            await SaveAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<UserAccountDetailResponse>.Conflict(ConcurrentAdministrationRefused);
        }

        return ApplicationResult<UserAccountDetailResponse>.Success(
            await LoadDetailAsync(user.Id, cancellationToken));
    }

    /// <summary>
    /// Atomic form of "another ACTIVE account still holds users.write". The invariant travels as
    /// the WHERE clause of one conditional UPDATE on <paramref name="userId"/>'s own row, so it is
    /// evaluated by the database at the instant the row is claimed rather than answered by an
    /// earlier SELECT that a concurrent commit can invalidate. Returns true only when the statement
    /// really matched the row.
    ///
    /// The single column it writes, <c>UpdatedAt</c>, is one the caller's mutation is about to
    /// stamp anyway with the very same timestamp: the claim adds no state of its own, it only needs
    /// to be a write so that the row is claimed, not merely read.
    /// </summary>
    private async Task<bool> TryClaimAnotherActiveAdministratorAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Users
            .Where(current => current.Id == userId
                && dbContext.Users.Any(other =>
                    other.Id != userId
                    && other.IsActive
                    && other.Roles.Any(userRole =>
                        userRole.Role.Permissions.Any(rolePermission =>
                            rolePermission.Permission.Key == PermissionCatalog.UsersWrite))))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    private static bool GrantsUserAdministration(User user)
    {
        return user.Roles
            .Select(userRole => userRole.Role)
            .Any(HasUserAdministration);
    }

    private static bool GrantsUserAdministration(IReadOnlyCollection<Role> roles)
    {
        return roles.Any(HasUserAdministration);
    }

    private static bool HasUserAdministration(Role role)
    {
        return role.Permissions.Any(rolePermission =>
            rolePermission.Permission.Key == PermissionCatalog.UsersWrite);
    }

    /// <summary>
    /// Resolves role names (case-insensitively, duplicates collapsed) into the tracked
    /// <see cref="Role"/> entities, with their permissions loaded so the guards can reason about
    /// what the new role set actually grants. An unknown name is a validation error, never a
    /// silently ignored one: an administrator who mistypes a role must not end up saving a user
    /// with fewer roles than intended.
    /// </summary>
    private async Task<ApplicationResult<IReadOnlyCollection<Role>>> ResolveRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        var requested = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (requested.Length == 0)
        {
            return ApplicationResult<IReadOnlyCollection<Role>>.Success([]);
        }

        var roles = await dbContext.Roles
            .Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => requested.Contains(role.Name))
            .ToArrayAsync(cancellationToken);

        var unknown = requested
            .Except(roles.Select(role => role.Name), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unknown.Length > 0)
        {
            return ApplicationResult<IReadOnlyCollection<Role>>.Validation(
                $"Unknown role(s): {string.Join(", ", unknown)}.");
        }

        return ApplicationResult<IReadOnlyCollection<Role>>.Success(roles);
    }

    /// <summary>
    /// Re-reads the user untracked after a mutation. The response must show the role names that
    /// were just assigned, and a freshly created <see cref="UserRole"/> only carries a RoleId -
    /// re-reading is what guarantees the projection never depends on change-tracker navigation
    /// fix-up having happened.
    /// </summary>
    private async Task<UserAccountDetailResponse> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await WithRolesAndPermissions(dbContext.Users.AsNoTracking())
            .SingleAsync(current => current.Id == id, cancellationToken);

        return MapDetail(user, DateTimeOffset.UtcNow);
    }

    private static IQueryable<User> WithRolesAndPermissions(IQueryable<User> query)
    {
        return query
            .Include(user => user.Roles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission);
    }

    private static UserAccountResponse Map(User user, DateTimeOffset now)
    {
        return new UserAccountResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword,
            user.LastLoginAt,
            user.IsLockedOut(now),
            user.LockedOutUntil,
            RoleNames(user));
    }

    private static UserAccountDetailResponse MapDetail(User user, DateTimeOffset now)
    {
        return new UserAccountDetailResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword,
            user.LastLoginAt,
            user.IsLockedOut(now),
            user.LockedOutUntil,
            RoleNames(user),
            EffectivePermissions(user),
            user.CreatedAt,
            user.CreatedBy,
            user.UpdatedAt,
            user.UpdatedBy);
    }

    private static string[] RoleNames(User user)
    {
        return user.Roles
            .Select(userRole => userRole.Role.Name)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string[] EffectivePermissions(User user)
    {
        return user.Roles
            .SelectMany(userRole => userRole.Role.Permissions)
            .Select(rolePermission => rolePermission.Permission.Key)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string NormalizeOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static string Describe(string before, string after)
    {
        return before == after ? after : $"'{before}' -> '{after}'";
    }

    /// <summary>
    /// Explicit flush after the audit write, mirroring BillingService and
    /// ApplicationSettingsService: AuditLogWriter.WriteAsync already saves, so this call is
    /// usually a no-op - it exists so persistence never silently depends on the audit writer's
    /// internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        User user,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                AuditEntityName,
                user.Id.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
