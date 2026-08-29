using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_is_true_for_a_token_that_has_not_expired_or_been_revoked()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash-value", BaseTime.AddDays(14), BaseTime);

        Assert.True(token.IsActive(BaseTime.AddDays(1)));
    }

    [Fact]
    public void IsActive_is_false_once_the_token_has_expired()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash-value", BaseTime.AddDays(14), BaseTime);

        Assert.False(token.IsActive(BaseTime.AddDays(15)));
    }

    [Fact]
    public void IsActive_is_false_after_the_token_has_been_revoked()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash-value", BaseTime.AddDays(14), BaseTime);

        token.Revoke(BaseTime.AddHours(1));

        Assert.False(token.IsActive(BaseTime.AddHours(2)));
    }

    [Fact]
    public void IsActive_is_true_just_before_expiry_and_false_at_and_after_expiry()
    {
        var expiresAt = BaseTime.AddDays(14);
        var token = new RefreshToken(Guid.NewGuid(), "hash-value", expiresAt, BaseTime);

        Assert.True(token.IsActive(expiresAt.AddMilliseconds(-1)));
        Assert.False(token.IsActive(expiresAt));
        Assert.False(token.IsActive(expiresAt.AddMilliseconds(1)));
    }
}
