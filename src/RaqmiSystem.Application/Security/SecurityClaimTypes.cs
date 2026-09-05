namespace RaqmiSystem.Application.Security;

public static class SecurityClaimTypes
{
    public const string Permission = "permission";

    /// <summary>
    /// Perimetre d'unites (lot 2.2). Le jeton porte SOIT un claim <c>scope</c> valant
    /// <see cref="GlobalScope"/>, SOIT un claim <see cref="Unit"/> par code d'unite autorise -
    /// jamais les deux. Codes seulement, pas de libelle : le jeton reste petit, et les routes
    /// recoivent de toute facon un code.
    /// </summary>
    public const string Scope = "scope";

    /// <summary>Un claim par code d'unite autorise, pour un perimetre restreint.</summary>
    public const string Unit = "unit";

    /// <summary>Valeur du claim <see cref="Scope"/> pour un perimetre global.</summary>
    public const string GlobalScope = "global";

    /// <summary>
    /// Valeur du claim <see cref="Scope"/> pour un perimetre restreint a AUCUNE unite : le
    /// compte a des affectations, mais aucune en validite. Ecrite explicitement pour qu'un tel
    /// jeton ne se confonde pas avec un jeton emis avant ce lot, qui ne porte aucun claim de
    /// perimetre et vaut global.
    /// </summary>
    public const string NoUnitScope = "none";
}
