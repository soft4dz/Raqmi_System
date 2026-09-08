namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Libellés courts des domaines (<see cref="DomainNode.ShortLabel"/>), pour la barre latérale
/// et les puces de domaine du catalogue de l'accueil.
/// </summary>
/// <remarks>
/// Règle éditoriale, vérifiée par les tests : au plus 22 caractères, unique, et chaque mot est
/// un mot du libellé officiel, un préfixe d'au moins quatre lettres d'un de ses mots
/// (« Admin ») ou un sigle déclaré (RH = Ressources Humaines). Jamais un synonyme : la barre
/// est la vue compacte du même vocabulaire, pas un second vocabulaire. Douze domaines gardent
/// leur nom entier ; ils figurent quand même ici pour que la table soit la liste complète des
/// 22 domaines, relue d'un seul regard.
///
/// La table vit dans ce fichier, à part des lignes <c>Domain("NN", …)</c> de
/// <c>FunctionalArchitectureCatalog.cs</c> que l'outillage lit par regex : leur forme ne
/// change pas.
/// </remarks>
public static partial class FunctionalArchitectureCatalog
{
    /// <summary>Longueur maximale d'un libellé court : ce qui tient sur une ligne de la barre latérale.</summary>
    public const int ShortLabelMaxLength = 22;

    /// <summary>
    /// Libellé court d'un domaine du catalogue. Une erreur d'édition (identifiant inconnu)
    /// est une incohérence à connaître dès la construction de l'arbre, pas un cas à tolérer.
    /// </summary>
    public static string ShortLabelFor(string domainId) =>
        ShortLabels.ById.TryGetValue(domainId, out var shortLabel)
            ? shortLabel
            : throw new KeyNotFoundException($"Le domaine '{domainId}' n'a aucun libellé court déclaré.");

    // Type imbriqué plutôt que champ statique de la classe partielle : l'ordre d'initialisation
    // des champs statiques répartis sur plusieurs fichiers partiels n'est pas défini, et
    // BuildTree (initialiseur de Tree, dans FunctionalArchitectureCatalog.cs) lit cette table.
    // Un type imbriqué s'initialise à son premier accès, d'où qu'il vienne.
    private static class ShortLabels
    {
        public static readonly IReadOnlyDictionary<string, string> ById = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01"] = "Mon Espace",
            ["02"] = "Admin & Socle ERP",
            ["03"] = "Finance & Comptabilité",
            ["04"] = "Commercial & CRM",
            ["05"] = "Facturation & Ventes",
            ["06"] = "PMS / Hébergement",
            ["07"] = "Revenue Management",
            ["08"] = "Housekeeping",
            ["09"] = "Groupes & MICE",
            ["10"] = "F&B / Restauration",
            ["11"] = "Stocks & Économat",
            ["12"] = "Achats & Fournisseurs",
            ["13"] = "RH & Paie",
            ["14"] = "Maintenance",
            ["15"] = "Qualité & Audit",
            ["16"] = "Juridique & Conformité",
            ["17"] = "GED / Documentaire",
            ["18"] = "PortMaster / Marina",
            ["19"] = "Parking & Accès",
            ["20"] = "Pilotage, KPI & BI",
            ["21"] = "Intégrations",
            ["22"] = "Administration Système"
        };
    }
}
