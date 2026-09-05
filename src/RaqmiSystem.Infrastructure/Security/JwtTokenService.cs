using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public LoginResponse CreateToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        IUnitScope unitScope,
        string refreshToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("display_name", user.DisplayName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(SecurityClaimTypes.Permission, permission)));

        // Perimetre (lot 2.2) : SOIT scope=global, SOIT un claim unit par code autorise. Le
        // jeton ne porte que des codes - pas de libelle, pas d'identifiant - pour rester petit,
        // et le filtre de route compare le code recu a cette liste sans aller en base. Le cas
        // degenere - restreint mais sans aucune unite en validite - est ecrit scope=none, pour
        // ne pas ressembler a un jeton d'avant ce lot (aucun claim de perimetre, lu global).
        if (unitScope.IsGlobal)
        {
            claims.Add(new Claim(SecurityClaimTypes.Scope, SecurityClaimTypes.GlobalScope));
        }
        else if (unitScope.AllowedUnitCodes.Count == 0)
        {
            claims.Add(new Claim(SecurityClaimTypes.Scope, SecurityClaimTypes.NoUnitScope));
        }
        else
        {
            claims.AddRange(unitScope.AllowedUnitCodes.Select(code => new Claim(SecurityClaimTypes.Unit, code)));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Audience = _options.Audience,
            Issuer = _options.Issuer,
            Expires = expiresAt.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        var accessToken = handler.CreateToken(descriptor);

        return new LoginResponse(
            accessToken,
            expiresAt,
            refreshToken,
            new AuthenticatedUser(
                user.Id,
                user.UserName,
                user.Email,
                user.DisplayName,
                user.MustChangePassword,
                roles,
                permissions,
                new UnitScopeResponse(unitScope.IsGlobal, unitScope.AllowedUnitCodes)));
    }
}
