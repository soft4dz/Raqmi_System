namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Forme de transport du perimetre d'unites : la reponse de connexion et <c>GET /api/v1/me</c>
/// la renvoient telle quelle. <paramref name="Units"/> est vide quand <paramref name="IsGlobal"/>
/// est vrai - le client affiche alors « Toutes les unites », jamais une liste.
/// </summary>
public sealed record UnitScopeResponse(
    bool IsGlobal,
    IReadOnlyCollection<string> Units);
