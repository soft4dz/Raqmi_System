using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les droits du profil connecte, tels que le moteur les applique indicateur par indicateur.
///
/// LE PRINCIPE : un indicateur n'est rendu que si l'utilisateur detient TOUTES les permissions
/// des modules dont il lit les donnees. Un ratio ne doit jamais servir de chemin detourne vers
/// une donnee interdite - la masse salariale rapportee au chiffre d'affaires reste une donnee de
/// paie, et un profil sans <c>hr.read</c> ne la voit pas, meme deguisee en pourcentage. Le filtre
/// est applique DANS LE SERVICE, avant que la valeur ne quitte le serveur : passer par l'API
/// plutot que par l'ecran ne change rien.
///
/// CE QUE CE MECANISME NE FAIT PAS, ET IL FAUT LE SAVOIR : il filtre par MODULE, pas par UNITE.
/// Raqmi System ne rattache aujourd'hui aucun utilisateur a un etablissement - ni l'entite
/// User, ni les jetons emis ne portent de perimetre d'unite - de sorte qu'un directeur d'unite
/// qui detient <c>lodging.read</c> lit deja l'occupation de toutes les unites par les endpoints
/// existants. Restreindre un profil a son etablissement demande un perimetre utilisateur dans le
/// socle de securite, qui vaudrait alors pour les vingt-neuf modules ; l'ajouter ici seulement
/// donnerait l'illusion d'un cloisonnement que le reste du produit n'applique pas.
/// </summary>
public sealed class KpiAccessContext
{
    private readonly IReadOnlySet<string> permissions;
    private readonly bool unrestricted;

    public KpiAccessContext(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        this.permissions = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        unrestricted = false;
    }

    private KpiAccessContext()
    {
        permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        unrestricted = true;
    }

    /// <summary>
    /// Contexte detenant toutes les permissions. Reserve aux tests et aux taches internes -
    /// notamment la pose planifiee d'instantanes, qui doit historiser la bibliotheque entiere
    /// sans dependre du profil de qui la declenche. Le chemin HTTP construit TOUJOURS son
    /// contexte a partir des revendications reelles du jeton.
    /// </summary>
    public static KpiAccessContext Unrestricted { get; } = new();

    public bool Has(string permission) => unrestricted || permissions.Contains(permission);

    /// <summary>
    /// Le profil peut-il lire cet indicateur ? Toutes les permissions exigees, pas une seule :
    /// un indicateur qui croise deux modules exige les deux, faute de quoi il revelerait la
    /// moitie interdite par soustraction.
    /// </summary>
    public bool CanRead(KpiDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.RequiredPermissions.All(Has);
    }
}
