namespace RaqmiSystem.Application.Identity;

/// <summary>
/// Remplacement complet du perimetre d'un compte : les unites absentes de <paramref name="Units"/>
/// sont retirees, les nouvelles ajoutees, celles deja la gardent leur date d'affectation. Une
/// liste vide est legitime et rend le compte GLOBAL (aucune affectation = tout voir, voir
/// UserUnitAssignment) - mais le champ doit etre present, pour qu'un corps qui l'a oublie soit
/// refuse plutot que lu comme « ouvrir toutes les unites ».
/// </summary>
public sealed record SetUserUnitsRequest(IReadOnlyCollection<string> Units);
