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
                permissions));
    }
}
