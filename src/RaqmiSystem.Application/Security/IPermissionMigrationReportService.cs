namespace RaqmiSystem.Application.Security;

/// <summary>
/// Etat de migration d'un role PERSONNALISE vers le modele de permissions cible
/// <c>domaine.ressource.action</c>.
/// </summary>
/// <param name="Name">Cle stable du role.</param>
/// <param name="DisplayName">Libelle affiche.</param>
/// <param name="IsActive">Le role est-il encore actif.</param>
/// <param name="LegacyKeysHeld">Cles historiques detenues (triees).</param>
/// <param name="TargetKeysHeld">Cles cibles deja detenues (triees).</param>
/// <param name="TargetKeysMissing">
/// Cles cibles que les cles historiques detenues COUVRENT mais que le role ne detient pas
/// encore : c'est la liste a accorder pour que le role soit migre sans perte ni extension.
/// </param>
/// <param name="IsMigrated">Vrai quand aucune cle cible ne manque.</param>
public sealed record PermissionMigrationRoleReport(
    string Name,
    string DisplayName,
    bool IsActive,
    IReadOnlyCollection<string> LegacyKeysHeld,
    IReadOnlyCollection<string> TargetKeysHeld,
    IReadOnlyCollection<string> TargetKeysMissing,
    bool IsMigrated);

/// <summary>
/// Rapport de migration des roles personnalises. Les roles SYSTEME n'y figurent pas : le seeder
/// les migre lui-meme a chaque demarrage, il n'y a rien a decider pour eux.
/// </summary>
/// <param name="GeneratedAt">Horodatage de generation (UTC).</param>
/// <param name="LegacyKeyCount">Nombre de cles historiques couvertes par le registre.</param>
/// <param name="TargetKeyCount">Nombre de cles cibles du registre.</param>
/// <param name="Roles">Un rapport par role non systeme, tries par nom.</param>
public sealed record PermissionMigrationReport(
    DateTimeOffset GeneratedAt,
    int LegacyKeyCount,
    int TargetKeyCount,
    IReadOnlyCollection<PermissionMigrationRoleReport> Roles);

/// <summary>
/// Lecture seule : dresse, pour chaque role personnalise, les cles historiques detenues et les
/// cles cibles qui lui manquent. Le rapport precede toujours la migration (livrable 6, risque
/// R02) : le seeder ne touche jamais un role personnalise, parce qu'une installation a pu lui
/// donner un sens que le registre ignore, et c'est l'administrateur qui accorde ensuite les cles
/// manquantes depuis ce rapport.
/// </summary>
public interface IPermissionMigrationReportService
{
    Task<PermissionMigrationReport> BuildAsync(CancellationToken cancellationToken);
}
