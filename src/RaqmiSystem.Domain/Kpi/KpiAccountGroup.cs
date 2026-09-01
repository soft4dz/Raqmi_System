namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Regroupement de gestion d'un compte du plan comptable, pour construire le compte de resultat
/// d'exploitation hotelier (GOP, EBE, marges) a partir des ecritures comptabilisees.
///
/// POURQUOI CE MAILLON EXISTE : le plan comptable SCF donne la CLASSE d'un compte (6 = charge,
/// 7 = produit), ce qui suffit a dire si un compte est une charge, mais pas a dire de QUELLE
/// charge il s'agit. Or le GOP se definit precisement par ce qu'il exclut : les dotations, le
/// resultat financier, l'impot et les charges fixes de propriete. Sans ce classement, un
/// "resultat" affiche serait le resultat comptable complet presente sous le nom de GOP - un
/// chiffre faux sous un nom juste.
///
/// Le module ne SEME AUCUN mapping et n'invente aucun numero de compte, pour la meme raison
/// que <c>AccountClassCatalog</c> ne livre pas de plan comptable : la nomenclature reelle est
/// une donnee de l'etablissement, saisie et verifiee par son comptable. Tant qu'aucun mapping
/// n'est configure, les indicateurs de resultat repondent
/// <see cref="KpiQuality.MissingData"/> - jamais zero.
///
/// La structure suit la logique USALI, qui est la grammaire du controle de gestion hotelier :
/// <code>
/// Revenue - DepartmentalExpense                       = marge brute departementale
/// marge departementale - UndistributedExpense         = GOP
/// GOP - FixedCharge                                   = EBE / EBITDA
/// EBE - DepreciationAndProvision - FinancialResult
///     - IncomeTax                                     = resultat net
/// </code>
/// </summary>
public enum KpiAccountGroup
{
    /// <summary>Produits d'exploitation : hebergement, restauration, boissons, autres ventes.</summary>
    Revenue = 1,

    /// <summary>
    /// Charges directes des departements operationnels : denrees, boissons, blanchisserie,
    /// personnel affecte a un departement.
    /// </summary>
    DepartmentalExpense = 2,

    /// <summary>
    /// Charges non reparties : administration, commercial, energie, entretien, systemes.
    /// Elles se soustraient de la marge departementale pour donner le GOP.
    /// </summary>
    UndistributedExpense = 3,

    /// <summary>
    /// Charges fixes de propriete : loyers, taxes fonciere et d'activite, assurances,
    /// redevances de gestion. Elles separent le GOP de l'EBE.
    /// </summary>
    FixedCharge = 4,

    /// <summary>Dotations aux amortissements, provisions et pertes de valeur.</summary>
    DepreciationAndProvision = 5,

    /// <summary>Produits et charges financiers.</summary>
    FinancialResult = 6,

    /// <summary>Impots sur le resultat.</summary>
    IncomeTax = 7
}
