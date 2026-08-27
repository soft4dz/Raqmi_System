namespace RaqmiSystem.Application.Identity;

public sealed record LoginRequest(
    string UserNameOrEmail,
    string Password);
