namespace RaqmiSystem.Domain.Lodging;

public enum ChargeKind
{
    /// <summary>Une nuitee d'hebergement, posee par le night audit. Toujours positive.</summary>
    Night,

    /// <summary>Toute prestation consommee pendant le sejour (minibar, restaurant, ...). Toujours positive.</summary>
    Extra,

    /// <summary>
    /// Un reglement impute au folio, referencant la piece de tresorerie qu'il reflete. Enregistre
    /// en NEGATIF pour que le solde du folio converge vers zero.
    /// </summary>
    Settlement,

    /// <summary>Un geste commercial ou une correction. Seule autre nature autorisee a etre negative.</summary>
    Adjustment,

    /// <summary>
    /// Une taxe posee separement de la prestation qu'elle accompagne : taxe de sejour, taxe
    /// municipale. Toujours positive, et distinguee de l'extra parce qu'elle ne se remise pas et
    /// qu'elle part souvent sur un compte comptable different.
    /// </summary>
    Tax,

    /// <summary>
    /// Une composante d'un forfait, posee a sa valeur de ventilation interne. Toujours positive.
    /// Distinguee de l'extra pour que le controle de gestion puisse lire ce que le forfait a
    /// reellement produit par service.
    /// </summary>
    Package
}
