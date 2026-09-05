using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Lot 2.2 - le modele du perimetre utilisateur <-> unite, hors HTTP : la valeur
/// <see cref="UnitScope"/>, l'entite <see cref="UserUnitAssignment"/> (normalisation, validite,
/// unicite tenue par la base) et la lecture en base <see cref="UnitScopeProvider"/> qui applique
/// la regle « aucune affectation = global, au moins une = restreint aux affectations en
/// validite ».
/// </summary>
public sealed class UnitScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_global_scope_allows_every_unit_and_lists_none()
    {
        var scope = UnitScope.Global;

        Assert.True(scope.IsGlobal);
        Assert.Empty(scope.AllowedUnitCodes);
        Assert.True(scope.Allows("ANY"));
        Assert.True(scope.Allows("  any "));
        Assert.True(scope.Allows(null));
    }

    [Fact]
    public void A_restricted_scope_normalizes_its_codes_and_compares_without_case_or_spaces()
    {
        var scope = UnitScope.Restricted(["hotelB", " HOTELA ", "hotela", "", "   "]);

        Assert.False(scope.IsGlobal);
        Assert.Equal(new[] { "HOTELA", "HOTELB" }, scope.AllowedUnitCodes);

        Assert.True(scope.Allows("HOTELA"));
        Assert.True(scope.Allows("hotelb"));
        Assert.True(scope.Allows(" HotelA "));
        Assert.False(scope.Allows("HOTELC"));
        Assert.False(scope.Allows(null));
        Assert.False(scope.Allows(""));
    }

    [Fact]
    public void A_restricted_scope_without_unit_is_not_global_and_allows_nothing()
    {
        var scope = UnitScope.Restricted([]);

        Assert.False(scope.IsGlobal);
        Assert.Empty(scope.AllowedUnitCodes);
        Assert.False(scope.Allows("HOTELA"));
    }

    [Fact]
    public void An_assignment_normalizes_the_unit_code_and_requires_an_author()
    {
        var assignment = new UserUnitAssignment(Guid.NewGuid(), " hotela ", "  admin ", Now);

        Assert.Equal("HOTELA", assignment.HotelUnitCode);
        Assert.Equal("admin", assignment.AssignedBy);
        Assert.Equal(Now, assignment.AssignedAt);
        Assert.Null(assignment.ValidFrom);
        Assert.Null(assignment.ValidTo);

        Assert.Throws<ArgumentException>(() => new UserUnitAssignment(Guid.NewGuid(), "HOTELA", " ", Now));
        Assert.Throws<ArgumentException>(() => new UserUnitAssignment(Guid.NewGuid(), " ", "admin", Now));
        Assert.Throws<ArgumentException>(() => new UserUnitAssignment(Guid.NewGuid(), "HOTELA", "admin", Now, Now, Now));
    }

    [Fact]
    public void An_assignment_is_effective_only_inside_its_validity_window()
    {
        var open = new UserUnitAssignment(Guid.NewGuid(), "HOTELA", "admin", Now);
        Assert.True(open.IsEffectiveAt(Now.AddYears(-10)));
        Assert.True(open.IsEffectiveAt(Now.AddYears(10)));

        var bounded = new UserUnitAssignment(Guid.NewGuid(), "HOTELA", "admin", Now, Now.AddDays(1), Now.AddDays(3));
        Assert.False(bounded.IsEffectiveAt(Now));
        Assert.True(bounded.IsEffectiveAt(Now.AddDays(1)));
        Assert.True(bounded.IsEffectiveAt(Now.AddDays(2)));
        // La fin est exclue : a l'instant meme de ValidTo, l'affectation ne compte plus.
        Assert.False(bounded.IsEffectiveAt(Now.AddDays(3)));
    }

    /// <summary>
    /// L'unicite (utilisateur, unite) est tenue par l'index unique, pas par le service : c'est ce
    /// qui protege deux administrateurs qui affecteraient la meme unite au meme moment.
    /// </summary>
    [Fact]
    public async Task The_same_unit_cannot_be_assigned_twice_to_the_same_user()
    {
        await using var dbContext = await CreateContextAsync();

        var user = await SeedUserAsync(dbContext, "scope.unique");
        await SeedUnitAsync(dbContext, "UNIQ");

        dbContext.Add(new UserUnitAssignment(user.Id, "UNIQ", "tests", Now));
        await dbContext.SaveChangesAsync();

        dbContext.Add(new UserUnitAssignment(user.Id, "uniq", "tests", Now.AddMinutes(1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task An_assignment_can_only_designate_an_existing_unit()
    {
        await using var dbContext = await CreateContextAsync();

        var user = await SeedUserAsync(dbContext, "scope.fk");

        dbContext.Add(new UserUnitAssignment(user.Id, "GHOST", "tests", Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task The_provider_reads_global_without_assignment_and_the_effective_units_otherwise()
    {
        await using var dbContext = await CreateContextAsync();

        var user = await SeedUserAsync(dbContext, "scope.provider");
        await SeedUnitAsync(dbContext, "PRVA");
        await SeedUnitAsync(dbContext, "PRVB");
        await SeedUnitAsync(dbContext, "PRVC");

        var provider = new UnitScopeProvider(dbContext);

        var global = await provider.GetForUserAsync(user.Id, Now, CancellationToken.None);
        Assert.True(global.IsGlobal);

        dbContext.Add(new UserUnitAssignment(user.Id, "PRVA", "tests", Now));
        dbContext.Add(new UserUnitAssignment(user.Id, "PRVB", "tests", Now, validTo: Now.AddDays(-1)));
        dbContext.Add(new UserUnitAssignment(user.Id, "PRVC", "tests", Now, validFrom: Now.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var restricted = await provider.GetForUserAsync(user.Id, Now, CancellationToken.None);
        Assert.False(restricted.IsGlobal);
        Assert.Equal(new[] { "PRVA" }, restricted.AllowedUnitCodes);

        // Un compte dont TOUTES les affectations sont hors validite (une expiree, une future) :
        // restreint a RIEN, jamais global par expiration.
        var expired = await SeedUserAsync(dbContext, "scope.provider.expired");
        dbContext.Add(new UserUnitAssignment(expired.Id, "PRVB", "tests", Now, validTo: Now.AddDays(-1)));
        dbContext.Add(new UserUnitAssignment(expired.Id, "PRVC", "tests", Now, validFrom: Now.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var none = await provider.GetForUserAsync(expired.Id, Now, CancellationToken.None);
        Assert.False(none.IsGlobal);
        Assert.Empty(none.AllowedUnitCodes);
    }

    private static async Task<RaqmiDbContext> CreateContextAsync()
    {
        // La connexion vit aussi longtemps que le contexte : la fermer detruit la base ":memory:".
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    private static async Task<User> SeedUserAsync(RaqmiDbContext dbContext, string userName)
    {
        var user = new User(userName, $"{userName}@example.com", userName, "hash", mustChangePassword: false);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static async Task SeedUnitAsync(RaqmiDbContext dbContext, string code)
    {
        dbContext.HotelUnits.Add(new HotelUnit(code, $"Unit {code}", HotelUnitType.Hotel));
        await dbContext.SaveChangesAsync();
    }
}
