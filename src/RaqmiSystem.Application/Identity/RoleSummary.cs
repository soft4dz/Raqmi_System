namespace RaqmiSystem.Application.Identity;

/// <summary>
/// A role offered by the role picker of the user administration screen. <paramref name="Name"/> is
/// the stable key (see RoleCatalog) that role-assignment requests carry; the display name and
/// description exist so the screen never has to hard-code French labels for system roles.
/// </summary>
public sealed record RoleSummary(
    string Name,
    string DisplayName,
    string Description,
    bool IsSystem);
