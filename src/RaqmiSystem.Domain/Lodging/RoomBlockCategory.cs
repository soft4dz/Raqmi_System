namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Famille de la cause d'une indisponibilite. Sert au reporting technique (quelle nature de
/// panne immobilise le parc) et au routage vers la maintenance ; le detail reste dans le motif
/// libre du blocage.
/// </summary>
public enum RoomBlockCategory
{
    /// <summary>Non classee.</summary>
    Unspecified = 0,

    /// <summary>Plomberie, sanitaires, evacuation.</summary>
    Plumbing = 1,

    /// <summary>Electricite, eclairage, prises.</summary>
    Electrical = 2,

    /// <summary>Climatisation, chauffage, ventilation.</summary>
    Hvac = 3,

    /// <summary>Mobilier, literie, menuiserie.</summary>
    Furniture = 4,

    /// <summary>Peinture, revetements, travaux de renovation.</summary>
    Renovation = 5,

    /// <summary>Nettoyage approfondi, desinsectisation, traitement.</summary>
    DeepCleaning = 6,

    /// <summary>Usage interne : logement de personnel, stockage, bureau temporaire.</summary>
    InternalUse = 7,

    /// <summary>Blocage administratif ou commercial decide par la direction.</summary>
    Administrative = 8,

    /// <summary>Degat des eaux, incendie, sinistre.</summary>
    Damage = 9
}
