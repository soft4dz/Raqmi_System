namespace RaqmiSystem.Application.Identity;

/// <summary>Une affectation utilisateur <-> unite, telle que l'ecran d'administration la montre.</summary>
public sealed record UserUnitAssignmentResponse(
    string HotelUnitCode,
    DateTimeOffset AssignedAt,
    string AssignedBy,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

/// <summary>
/// Le perimetre d'un compte vu par l'administration (<c>GET/PUT /security/users/{id}/units</c>).
/// <paramref name="IsGlobal"/> est vrai quand <paramref name="Units"/> est vide : c'est la
/// meme regle que le jeton, dite explicitement pour que l'ecran n'ait pas a la deduire.
/// </summary>
public sealed record UserUnitScopeResponse(
    Guid UserId,
    string UserName,
    bool IsGlobal,
    IReadOnlyCollection<UserUnitAssignmentResponse> Units);
