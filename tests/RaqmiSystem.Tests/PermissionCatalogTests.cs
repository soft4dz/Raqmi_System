using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Permission_keys_are_unique()
    {
        var keys = PermissionCatalog.All.Select(permission => permission.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Permission_keys_are_normalized()
    {
        Assert.All(PermissionCatalog.All, permission =>
        {
            Assert.Equal(permission.Key, permission.Key.ToLowerInvariant());
            Assert.DoesNotContain(" ", permission.Key);
        });
    }
}
