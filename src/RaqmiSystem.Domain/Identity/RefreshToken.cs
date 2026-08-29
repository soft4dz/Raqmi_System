namespace RaqmiSystem.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = RequireValue(tokenHash, nameof(tokenHash));
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive(DateTimeOffset utcNow)
    {
        return RevokedAt is null && ExpiresAt > utcNow;
    }

    public void Revoke(DateTimeOffset utcNow)
    {
        RevokedAt ??= utcNow;
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
