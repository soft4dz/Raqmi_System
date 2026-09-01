using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Le coeur du PMS : referentiel des chambres, recherche de disponibilite, cycle de vie d'une
/// reservation, deroulement d'un sejour et folios.
///
/// UNE SEULE SOURCE DE VERITE POUR L'INVENTAIRE. Toutes les operations qui vendent ou tiennent une
/// chambre - recherche, creation, walk-in, affectation, changement de chambre, prolongation,
/// changement de type - passent par le MEME calcul de disponibilite
/// (<see cref="AvailabilityCalculator"/>) alimente par les memes sources : parc physique, blocages
/// OOO/OOS, reservations, allotements, surreservation, restrictions. Deux chemins qui compteraient
/// differemment finiraient toujours par ne plus etre d'accord, et l'ecart se paierait en survente
/// silencieuse.
/// </summary>
public interface ILodgingService
{
    // ==================================== Types de chambres ====================================

    Task<IReadOnlyCollection<RoomTypeResponse>> ListRoomTypesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> GetRoomTypeAsync(Guid id, CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> CreateRoomTypeAsync(
        CreateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> UpdateRoomTypeAsync(
        Guid id,
        UpdateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> SetRoomTypeActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ======================================== Chambres ========================================

    Task<IReadOnlyCollection<RoomResponse>> ListRoomsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> GetRoomAsync(Guid id, CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> CreateRoomAsync(
        CreateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> UpdateRoomAsync(
        Guid id,
        UpdateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> SetRoomActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ====================================== Reservations ======================================

    /// <summary>
    /// Les filtres de periode utilisent la semantique du CHEVAUCHEMENT : un dossier est liste des
    /// que son sejour touche [from, to], et pas seulement quand il y commence.
    /// </summary>
    Task<IReadOnlyCollection<ReservationResponse>> ListReservationsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReservationStatus? status,
        string? customerCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> GetReservationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>La vue complete d'un dossier : folios, extras, acomptes, historique et journal.</summary>
    Task<ApplicationResult<ReservationDetailResponse>> GetReservationDetailAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recherche de disponibilite. Rend deux niveaux de lecture : la disponibilite COMMERCIALE par
    /// type - ce qu'on peut vendre, au prix resolu nuit par nuit - et les chambres physiques libres
    /// pour l'affectation. Les restrictions qui ferment la periode sont rendues explicitement :
    /// un ecran vide sans explication ferait croire a une occupation complete.
    /// </summary>
    Task<ApplicationResult<AvailabilityResponse>> GetAvailabilityAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int guests,
        string? customerCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recherche de disponibilite complete, avec composition des occupants, type, plan, canal et
    /// autorisation de surreservation.
    /// </summary>
    Task<ApplicationResult<AvailabilityResponse>> SearchAvailabilityAsync(
        AvailabilitySearchRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree un dossier. Le tarif de CHAQUE nuit est resolu par le module Tarifs (convention client
    /// appliquee, regle de yield tracee) puis FIGE dans le dossier, de sorte que le folio facture
    /// exactement ce que la recherche a annonce. La garde anti-double-reservation, les allotements,
    /// les restrictions et la surreservation sont controles ATOMIQUEMENT ici.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CreateReservationAsync(
        CreateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Walk-in : vente, affectation et arrivee dans le meme geste, tout ou rien. Le dossier porte
    /// la marque du walk-in, qui n'a ni la meme lecture commerciale ni le meme traitement de
    /// pre-arrivee qu'une reservation ordinaire.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CreateWalkInAsync(
        WalkInRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Met a jour les informations d'accompagnement du dossier : heures annoncees, composition,
    /// origine commerciale, notes. Les dates, le type et la chambre ont leur propre geste, parce
    /// qu'ils touchent l'inventaire et le prix.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> UpdateReservationAsync(
        Guid id,
        UpdateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Passage entre statuts d'avant-arrivee : demande, option, confirmee, garantie.</summary>
    Task<ApplicationResult<ReservationResponse>> ChangeReservationStatusAsync(
        Guid id,
        ChangeReservationStatusRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> SetGuaranteeAsync(
        Guid id,
        SetGuaranteeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Affecte ou libere la chambre physique d'un dossier vendu par type. La disponibilite de la
    /// chambre est revalidee sur TOUTE la periode, dans la meme transaction que l'ecriture.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> AssignRoomAsync(
        Guid id,
        AssignRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deplace un sejour vers une autre chambre. L'ancienne chambre passe en SALE cote
    /// housekeeping, l'historique conserve les deux, et le motif est obligatoire.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> MoveRoomAsync(
        Guid id,
        RoomMoveRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prolonge ou raccourcit un sejour. La nouvelle periode est revalidee entierement puis les
    /// tarifs sont reposes nuit par nuit ; l'ancien total reste au journal du sejour.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> ExtendStayAsync(
        Guid id,
        ExtendStayRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Surclassement ou declassement. Le SENS est deduit du rang des types, et l'ecart tarifaire
    /// est affiche puis, s'il est facture, repercute sur le folio.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> ChangeRoomTypeAsync(
        Guid id,
        ChangeRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enregistre l'arrivee. Ouvre le folio client, informe le housekeeping, applique le supplement
    /// d'arrivee anticipee quand la politique de l'unite en prevoit un.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CheckInAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// PREPARE le depart : pose les nuitees que le night audit n'a pas encore posees et le
    /// supplement de depart tardif, puis rend les folios a jour. Idempotent, et COMMITTE avant tout
    /// controle de solde - sinon un depart refuse annulerait le rattrapage et la reception verrait
    /// une note plus basse que ce que le client doit.
    /// </summary>
    Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> PrepareCheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enregistre le depart, refuse tant qu'un folio du sejour n'est pas solde. Prepare d'abord la
    /// note, passe ensuite la chambre en SALE et ferme les folios.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Annule un dossier. La penalite est calculee depuis la politique FIGEE dans le dossier, pas
    /// depuis la politique en vigueur aujourd'hui.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CancelReservationAsync(
        Guid id,
        CancelReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> MarkNoShowAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    // ========================================= Folios =========================================

    /// <summary>Le folio CLIENT du sejour. Conserve pour compatibilite ; voir ListFoliosAsync.</summary>
    Task<ApplicationResult<FolioResponse>> GetFolioAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>Tous les folios d'un sejour.</summary>
    Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> ListFoliosAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<FolioResponse>> CreateFolioAsync(
        Guid reservationId,
        CreateFolioRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Ajoute une ligne au folio vise (le folio client a defaut).</summary>
    Task<ApplicationResult<FolioResponse>> AddFolioChargeAsync(
        Guid reservationId,
        AddFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Deplace une ligne vers un autre folio du meme sejour, par contre-passation.</summary>
    Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> TransferFolioChargeAsync(
        Guid reservationId,
        TransferFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // ========================================= Extras =========================================

    Task<ApplicationResult<IReadOnlyCollection<ReservationExtraResponse>>> ListReservationExtrasAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationExtraResponse>> AddReservationExtraAsync(
        Guid reservationId,
        AddReservationExtraRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<bool>> RemoveReservationExtraAsync(
        Guid reservationId,
        Guid extraId,
        OperationContext context,
        CancellationToken cancellationToken);

    // ======================================== Acomptes ========================================

    Task<ApplicationResult<IReadOnlyCollection<DepositResponse>>> ListDepositsAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepositResponse>> CreateDepositAsync(
        Guid reservationId,
        CreateDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepositResponse>> PayDepositAsync(
        Guid depositId,
        PayDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Impute un acompte verse au folio client : le folio recoit une ligne de reglement.</summary>
    Task<ApplicationResult<DepositResponse>> ApplyDepositAsync(
        Guid depositId,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepositResponse>> RefundDepositAsync(
        Guid depositId,
        CloseDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepositResponse>> ForfeitDepositAsync(
        Guid depositId,
        CloseDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // ======================================= Occupation =======================================

    /// <summary>
    /// Occupation jour par jour d'une unite sur [from, to] inclus : chambres actives, chambres
    /// occupees par un sejour couvrant la nuit, et le pourcentage qui en decoule.
    /// </summary>
    Task<ApplicationResult<OccupancyResponse>> GetOccupancyAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>
    /// L'ecran de comptoir d'une unite pour une journee : arrivees, departs avec soldes, retards
    /// des deux cotes, presents pour la nuit et occupation du jour.
    /// </summary>
    Task<ApplicationResult<FrontDeskResponse>> GetFrontDeskAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken);
}
