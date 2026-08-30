namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Detail view of a single user: everything <see cref="UserAccountResponse"/> carries, plus the
/// EFFECTIVE permissions - the union of the permissions granted by the user's roles. Permissions
/// are never attached to a user directly in this system, so this union is the whole truth about
/// what the account can do, and it is what the screen must show rather than making the reader
/// mentally resolve role names against the permission catalog.
/// </summary>
public sealed record UserAccountDetailResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    bool IsLockedOut,
    DateTimeOffset? LockedOutUntil,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
