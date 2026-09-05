namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Le compte tel que la reponse de connexion le decrit : ce que le jeton porte, lisible sans
/// le decoder - roles, permissions et, depuis le lot 2.2, le perimetre d'unites.
/// </summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    UnitScopeResponse UnitScope);
