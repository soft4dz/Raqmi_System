using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Security;

/// <summary>
/// Lecture du perimetre en base (lot 2.2). La regle est celle de UserUnitAssignment : aucune
/// ligne = global ; au moins une ligne = restreint aux lignes en validite a cet instant.
///
/// La methode statique existe pour AuthenticationService, dont le constructeur est fige par
/// les tests qui l'instancient a la main : il lit le perimetre avec le contexte qu'il tient
/// deja, sans nouvelle dependance. L'instance enregistree par AddRaqmiUnitScope sert a ce qui
/// viendra apres - les services proprietaires, a la phase suivante.
/// </summary>
public sealed class UnitScopeProvider(RaqmiDbContext dbContext) : IUnitScopeProvider
{
    public Task<IUnitScope> GetForUserAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        return LoadAsync(dbContext, userId, utcNow, cancellationToken);
    }

    public static async Task<IUnitScope> LoadAsync(
        RaqmiDbContext dbContext,
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<UserUnitAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .ToArrayAsync(cancellationToken);

        if (assignments.Length == 0)
        {
            return UnitScope.Global;
        }

        return UnitScope.Restricted(assignments
            .Where(assignment => assignment.IsEffectiveAt(utcNow))
            .Select(assignment => assignment.HotelUnitCode));
    }
}
