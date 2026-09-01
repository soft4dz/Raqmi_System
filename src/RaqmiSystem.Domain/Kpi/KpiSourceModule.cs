namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Le module de Raqmi System qui POSSEDE la donnee dont l'indicateur est tire. C'est la
/// materialisation du principe fondamental du moteur : un KPI n'est jamais une seconde base
/// metier, il est toujours une lecture des transactions officielles d'un module existant.
///
/// Cette valeur sert a deux choses concretes : afficher au lecteur ou aller verifier un chiffre
/// qui le surprend, et rattacher l'indicateur a la permission de ce module (voir
/// <see cref="KpiDefinition.RequiredPermission"/>) - un utilisateur qui n'a pas le droit de
/// lire la paie ne doit pas la deviner a travers un ratio.
/// </summary>
public enum KpiSourceModule
{
    /// <summary>Recettes journalieres (DailyRevenue).</summary>
    DailyRevenue = 1,

    /// <summary>Hebergement : chambres, reservations, folios.</summary>
    Lodging = 2,

    /// <summary>Facturation : clients, factures.</summary>
    Billing = 3,

    /// <summary>Creances et recouvrement : balance agee.</summary>
    Receivables = 4,

    /// <summary>Tresorerie : encaissements, ordres de paiement, comptes bancaires.</summary>
    Treasury = 5,

    /// <summary>Comptabilite SCF : plan comptable, ecritures comptabilisees.</summary>
    Accounting = 6,

    /// <summary>Budget et previsions : objectifs mensuels par unite.</summary>
    Budgeting = 7,

    /// <summary>Stocks et consommations : mouvements valorises.</summary>
    Inventory = 8,

    /// <summary>Achats et approvisionnements : fournisseurs, bons de commande.</summary>
    Purchasing = 9,

    /// <summary>Cuisine : fiches techniques et cout matiere theorique.</summary>
    Kitchen = 10,

    /// <summary>Ressources humaines et paie.</summary>
    HumanResources = 11,

    /// <summary>Housekeeping : etat des chambres et taches de nettoyage.</summary>
    Housekeeping = 12,

    /// <summary>CRM : satisfaction, NPS, fidelite.</summary>
    Crm = 13,

    /// <summary>Cloture journaliere.</summary>
    Closing = 14,

    /// <summary>
    /// Aucun module de Raqmi System ne porte cette donnee aujourd'hui. Reserve aux indicateurs
    /// <see cref="KpiAvailability.AwaitingSource"/>.
    /// </summary>
    None = 99
}
