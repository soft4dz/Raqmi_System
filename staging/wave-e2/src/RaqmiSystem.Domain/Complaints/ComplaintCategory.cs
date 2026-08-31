namespace RaqmiSystem.Domain.Complaints;

/// <summary>
/// Functional area a complaint is about. The category is one of the two axes of the
/// root-cause analysis report, so it is a closed list rather than free text.
/// </summary>
public enum ComplaintCategory
{
    Chambre,
    Restauration,
    Accueil,
    Facturation,
    Bruit,
    Autre
}
