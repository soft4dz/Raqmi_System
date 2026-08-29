namespace RaqmiSystem.Application.Identity;

public interface IAuthenticationService
{
    Task<LoginResponse?> SignInAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<LoginResponse?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken);
}
