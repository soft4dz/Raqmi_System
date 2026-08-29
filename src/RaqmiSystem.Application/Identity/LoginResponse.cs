namespace RaqmiSystem.Application.Identity;

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    AuthenticatedUser User);
