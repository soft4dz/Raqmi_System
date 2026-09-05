namespace RaqmiSystem.Application.Security;

/// <summary>
/// Le perimetre d'unites d'un appelant, tel que le jeton le porte (lot 2.2).
///
/// Deux formes, et rien entre les deux : GLOBAL (aucune affectation utilisateur <-> unite :
/// les roles systeme et tous les comptes existants, qui voient tout comme avant ce lot) ou
/// RESTREINT a une liste de codes d'unite. La question qu'on lui pose est toujours la meme -
/// « cette unite est-elle dans mon perimetre ? » - et c'est <see cref="Allows"/> qui y repond,
/// pour le filtre de route aujourd'hui et pour les services proprietaires a la phase suivante.
/// </summary>
public interface IUnitScope
{
    /// <summary>Vrai quand l'appelant n'est restreint a aucune unite.</summary>
    bool IsGlobal { get; }

    /// <summary>
    /// Codes d'unite autorises, normalises (majuscules), tries. Vide pour un perimetre global -
    /// la liste n'a alors pas de sens, <see cref="IsGlobal"/> fait foi.
    /// </summary>
    IReadOnlyCollection<string> AllowedUnitCodes { get; }

    /// <summary>
    /// L'unite est-elle dans le perimetre ? Toujours vrai en global ; en restreint, vrai si le
    /// code (compare sans casse ni espaces) figure dans la liste, faux sinon - y compris pour
    /// un code vide : il n'y a alors aucune unite a autoriser.
    /// </summary>
    bool Allows(string? hotelUnitCode);
}
