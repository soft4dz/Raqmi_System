using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Creation d'un dossier de reservation.
///
/// VENTE PAR TYPE, AFFECTATION PLUS TARD. <paramref name="RoomTypeCode"/> est ce que le client
/// achete ; <paramref name="RoomId"/> est facultatif. Passer une chambre revient a l'affecter des
/// la vente - c'est le cas d'un walk-in ou d'une demande precise ; ne rien passer laisse le dossier
/// en attente d'affectation, ce qui est le fonctionnement normal d'un hotel. Quand une chambre est
/// passee SANS type, le type est deduit de la chambre : c'est ce qui garde compatibles les appels
/// existants.
///
/// <paramref name="AllotmentId"/> distingue les deux natures de vente, et cette distinction
/// commande tout le controle de disponibilite :
///   * null : vente PUBLIQUE. Elle ne doit pas entamer les chambres tenues pour un groupe ; le
///     service refuse si la prendre passerait sous le solde d'un allotement.
///   * renseigne : vente SUR UN BLOC. Elle consomme le bloc, qui avait deja retire la chambre de
///     la vente publique - la compter une seconde fois interdirait de vendre des chambres libres.
///
/// <paramref name="AllowOverbooking"/> n'ouvre la surreservation que si l'unite l'autorise ET si
/// l'appelant en a le droit : les deux conditions doivent etre vraies, jamais une seule.
/// </summary>
public sealed record CreateReservationRequest(
    string HotelUnitCode,
    Guid? RoomId,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int GuestCount,
    Guid? AllotmentId = null,
    string? GuestName = null,
    string? RoomTypeCode = null,
    int? Adults = null,
    int Children = 0,
    int Infants = 0,
    TimeOnly? EstimatedArrivalTime = null,
    TimeOnly? EstimatedDepartureTime = null,
    ReservationStatus Status = ReservationStatus.Confirmed,
    string? MarketSegmentCode = null,
    string? ChannelCode = null,
    string? SourceCode = null,
    string? CompanyCode = null,
    string? AgencyCode = null,
    string? Notes = null,
    string? SpecialRequests = null,
    GuaranteeKind Guarantee = GuaranteeKind.None,
    string? GuaranteeReference = null,
    string? CancellationPolicyCode = null,
    bool AllowOverbooking = false,
    bool OverrideRestrictions = false);
