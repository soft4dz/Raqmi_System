using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Application.Security;

public interface ITokenService
{
    LoginResponse CreateToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string refreshToken);
}
