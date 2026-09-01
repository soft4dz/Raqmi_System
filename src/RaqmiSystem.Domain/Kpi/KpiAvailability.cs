namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Un indicateur du catalogue est-il calculable par cette version de Raqmi System ?
///
/// Le catalogue declare la bibliotheque COMPLETE attendue d'un ERP hotelier, y compris les
/// indicateurs dont la donnee source n'existe pas encore dans le produit (MTTR sans module
/// GMAO, ticket moyen sans point de vente). Les declarer est utile - la formule, l'unite et la
/// regle d'agregation sont fixees une fois pour toutes, et l'ecran sait quoi afficher le jour
/// ou le module arrive. Les CALCULER serait un mensonge.
///
/// Un indicateur <see cref="AwaitingSource"/> renvoie donc toujours une valeur nulle avec la
/// qualite <see cref="KpiQuality.NotApplicable"/> et le nom exact du module qui lui manque.
/// </summary>
public enum KpiAvailability
{
    /// <summary>Calcule par le moteur a partir des donnees transactionnelles existantes.</summary>
    Implemented = 1,

    /// <summary>
    /// Declare, documente, mais non calculable : la source designee par
    /// <see cref="KpiDefinition.MissingSource"/> n'existe pas encore dans le produit.
    /// </summary>
    AwaitingSource = 2
}
