using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verify_returns_true_for_the_original_password()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.Hash("StrongPassword-2026!");

        Assert.True(hasher.Verify("StrongPassword-2026!", hash));
    }

    [Fact]
    public void Verify_returns_false_for_a_wrong_password()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.Hash("StrongPassword-2026!");

        Assert.False(hasher.Verify("WrongPassword-2026!", hash));
    }

    [Fact]
    public void Hash_uses_a_unique_salt_each_time()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var firstHash = hasher.Hash("StrongPassword-2026!");
        var secondHash = hasher.Hash("StrongPassword-2026!");

        Assert.NotEqual(firstHash, secondHash);
    }
}
