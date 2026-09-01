using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Security;

/// <summary>
/// Implementation en lecture seule de <see cref="IPermissionMigrationReportService"/> : une
/// seule requete sur les roles non systeme et leurs permissions, puis le calcul se fait contre
/// <see cref="PermissionRegistry"/>, en memoire. Rien n'est ecrit, rien n'est audite : le
/// rapport est un constat, la migration qui le suit est un acte d'administration des roles.
/// </summary>
public sealed class PermissionMigrationReportService(RaqmiDbContext dbContext) : IPermissionMigrationReportService
{
    public async Task<PermissionMigrationReport> BuildAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(role => !role.IsSystem)
            .Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .OrderBy(role => role.Name)
            .ToArrayAsync(cancellationToken);

        var rows = roles.Select(Describe).ToArray();

        return new PermissionMigrationReport(
            DateTimeOffset.UtcNow,
            PermissionRegistry.LegacyKeys.Count,
            PermissionRegistry.All.Count,
            rows);
    }

    private static PermissionMigrationRoleReport Describe(Role role)
    {
        var held = role.Permissions
            .Select(rolePermission => rolePermission.Permission.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var legacyHeld = held
            .Where(PermissionRegistry.IsLegacyKey)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var targetHeld = held
            .Where(PermissionRegistry.IsTargetKey)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Strictement ce que les cles historiques detenues couvrent : ni plus (aucune extension
        // proposee), ni moins (aucune perte a la migration).
        var targetMissing = legacyHeld
            .SelectMany(PermissionRegistry.TargetKeysCoveredBy)
            .Distinct(StringComparer.Ordinal)
            .Where(targetKey => !held.Contains(targetKey))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PermissionMigrationRoleReport(
            role.Name,
            role.DisplayName,
            role.IsActive,
            legacyHeld,
            targetHeld,
            targetMissing,
            targetMissing.Length == 0);
    }
}
