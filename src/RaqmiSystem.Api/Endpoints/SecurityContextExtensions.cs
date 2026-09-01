using System.Security.Claims;
using RaqmiSystem.Application.Security;

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
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permissionKey)
    {
        return user.HasClaim(SecurityClaimTypes.Permission, permissionKey);
    }
}
