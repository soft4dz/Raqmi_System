namespace RaqmiSystem.Domain.Identity;

/// <summary>
/// Une permission du modele cible <c>domaine.ressource.action</c> (voir <see cref="PermissionRegistry"/>).
///
/// <paramref name="Key"/> est la cle telle qu'elle voyage dans le JWT et dans les politiques
/// (<c>finance.entry.post</c>). Son premier segment est le prefixe du domaine (<c>finance</c>),
/// pas son identifiant : l'identifiant stable du domaine fonctionnel ("01" a "22") est porte a
/// part par <paramref name="Domain"/>, parce qu'un prefixe lisible et un identifiant stable ne
/// changent pas pour les memes raisons.
///
/// <paramref name="LegacyKeys"/> sont les cles historiques qui COUVRENT cette cle : en detenir
/// une vaut la detenir. La liste peut etre vide quand la cle historique etait deja au format
/// cible (<c>hr.payroll.close</c>) - la cle est alors sa propre cible.
/// </summary>
public sealed record TargetPermissionDefinition(
    string Key,
    string Domain,
    string Resource,
    string Action,
    string Name,
    string Description,
    IReadOnlyCollection<string> LegacyKeys)
{
    /// <summary>
    /// Le prefixe de domaine de la cle (<c>finance</c> pour <c>finance.entry.post</c>). Sert de
    /// categorie d'affichage dans le catalogue, comme <c>PermissionDefinition.Category</c> pour
    /// les cles historiques.
    /// </summary>
    public string Prefix => Key[..Key.IndexOf('.')];
}
