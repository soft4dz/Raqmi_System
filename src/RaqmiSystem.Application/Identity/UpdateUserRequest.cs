namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Update payload. It carries no user name: the login identifier is immutable (see
/// User.UpdateProfile), and roles and activation are separate, individually audited operations
/// rather than fields silently rewritten by a general-purpose save.
/// </summary>
public sealed record UpdateUserRequest(
    string Email,
    string DisplayName);
