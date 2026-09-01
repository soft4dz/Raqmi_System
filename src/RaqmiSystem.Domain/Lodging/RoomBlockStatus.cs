namespace RaqmiSystem.Domain.Lodging;

/// <summary>Cycle de vie d'un blocage de chambre.</summary>
public enum RoomBlockStatus
{
    /// <summary>
    /// Programme : le blocage porte sur une periode qui retire deja la chambre de la vente,
    /// meme si elle n'a pas encore commence. C'est voulu - on bloque justement A L'AVANCE pour
    /// ne pas vendre une chambre qu'on sait indisponible.
    /// </summary>
    Planned = 0,

    /// <summary>En cours : la periode courante est couverte.</summary>
    Active = 1,

    /// <summary>Remis en service : la chambre est revenue a la vente, a sa date reelle de retour.</summary>
    Closed = 2,

    /// <summary>Annule : le blocage n'a jamais eu lieu et ne retire plus rien de l'inventaire.</summary>
    Cancelled = 3
}
