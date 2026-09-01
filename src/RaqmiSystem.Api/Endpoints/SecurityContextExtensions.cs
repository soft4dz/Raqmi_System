using System.Security.Claims;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class SecurityContextExtensions
{
    public static OperationContext ToOperationContext(this HttpContext httpContext)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : (Guid?)null;
        var userName = httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? "unknown";

        return new OperationContext(
            userId,
            userName,
            httpContext.Connection.RemoteIpAddress?.ToString());
    }

    /// <summary>
    /// L'appelant detient-il cette permission ?
    ///
    /// SERT AUX LEVIERS OPTIONNELS, PAS AU CONTROLE D'ACCES. L'acces a une route reste tenu par
    /// RequireAuthorization ; ce test-ci sert aux drapeaux qu'une requete peut demander mais que
    /// tout le monde n'a pas le droit d'activer - la surreservation, la levee d'une restriction.
    /// Sans lui, un poste de reception vendrait au-dela de la capacite en cochant une case, alors
    /// que la meme case est legitime pour un responsable d'unite.
    ///
    /// La cle est evaluee avec la MEME regle que la politique d'autorisation qui porte son nom
    /// (PermissionRegistry.AcceptedClaims) : demander la cle cible accepte aussi la cle
    /// historique qui la couvre, et jamais l'inverse pour une cle composite. Un levier ne doit
    /// pas s'ouvrir a un profil que la route equivalente refuserait.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permissionKey)
    {
        var acceptedClaims = PermissionRegistry.AcceptedClaims(permissionKey);

        return user.Claims.Any(claim =>
            claim.Type == SecurityClaimTypes.Permission
            && acceptedClaims.Contains(claim.Value, StringComparer.Ordinal));
    }
}
