namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Mise a jour des informations D'ACCOMPAGNEMENT d'un dossier : heures annoncees, composition des
/// occupants, origine commerciale, notes et demandes.
///
/// CE QUI N'EST PAS ICI EST VOLONTAIRE. Les dates, le type et la chambre ont chacun leur propre
/// route, parce qu'ils touchent l'INVENTAIRE et la TARIFICATION : les changer suppose de revalider
/// la disponibilite, de reposer les tarifs et de journaliser le geste. Les melanger a une simple
/// correction de note ferait passer une revente pour une modification anodine.
/// </summary>
public sealed record UpdateReservationRequest(
    int Adults,
    int Children = 0,
    int Infants = 0,
    TimeOnly? EstimatedArrivalTime = null,
    TimeOnly? EstimatedDepartureTime = null,
    string? MarketSegmentCode = null,
    string? ChannelCode = null,
    string? SourceCode = null,
    string? CompanyCode = null,
    string? AgencyCode = null,
    string? GuestName = null,
    string? Notes = null,
    string? SpecialRequests = null);
