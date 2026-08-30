namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Full replacement of a user's role set: whatever is absent from <paramref name="Roles"/> is
/// revoked. An empty collection is legitimate and strips every role - but the field itself must be
/// present, so that a body that simply forgot it is rejected instead of silently stripping them.
/// </summary>
public sealed record SetUserRolesRequest(IReadOnlyCollection<string> Roles);
