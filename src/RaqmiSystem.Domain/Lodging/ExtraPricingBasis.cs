namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Sur quoi se calcule le prix d'un extra. C'est la seule information qui permet de poser
/// automatiquement la bonne ligne : un lit d'appoint se facture par nuit, un transfert une fois
/// pour le sejour, un petit-dejeuner par personne et par nuit. Sans elle, il faudrait ressaisir
/// la quantite a chaque fois - et l'erreur passerait inapercue jusqu'au depart.
/// </summary>
public enum ExtraPricingBasis
{
    /// <summary>Une fois pour tout le sejour (transfert aeroport, forfait menage).</summary>
    PerStay = 0,

    /// <summary>Une fois par nuit (lit d'appoint, parking).</summary>
    PerNight = 1,

    /// <summary>Une fois par personne, pour le sejour (carte de plage, acces spa).</summary>
    PerPerson = 2,

    /// <summary>Par personne et par nuit (petit-dejeuner, demi-pension).</summary>
    PerPersonPerNight = 3,

    /// <summary>A la quantite saisie (minibar, blanchisserie, telephone).</summary>
    PerQuantity = 4
}
