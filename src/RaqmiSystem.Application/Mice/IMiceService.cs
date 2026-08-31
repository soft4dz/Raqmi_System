using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Module 10.6 - Groupes et MICE. Le module couvre desormais ses deux volets.
///
/// VOLET SALLES : espaces de reception, evenements, devis, BEO, facturation evenementielle. Une
/// salle se vend au creneau et non a la nuitee ; elle n'entre ni dans la disponibilite chambres ni
/// dans le taux d'occupation.
///
/// VOLET GROUPES : allotements et rooming lists. Celui-la touche le coeur du PMS, et c'est ce qui
/// le rend delicat. Un allotement retire des chambres de la vente SANS les nommer : la recherche de
/// disponibilite doit en soustraire le solde ET la creation de reservation doit refuser de
/// l'entamer. Les deux chemins partagent volontairement un unique calcul, dans LodgingService : les
/// laisser diverger ferait survendre l'hotel en silence, la recherche affichant moins de chambres
/// que la creation n'en accepte.
/// </summary>
public interface IMiceService
{
    // ------------------------------- Espaces de reception -------------------------------

    Task<IReadOnlyCollection<FunctionSpaceResponse>> ListFunctionSpacesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<FunctionSpaceResponse>> CreateFunctionSpaceAsync(
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<FunctionSpaceResponse>> UpdateFunctionSpaceAsync(
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Active ou desactive un espace. Une desactivation ne touche PAS les evenements deja places :
    /// annuler le mariage d'un client parce qu'une salle a ete archivee serait bien pire qu'une
    /// salle inactive portant encore un evenement.
    /// </summary>
    Task<ApplicationResult<FunctionSpaceResponse>> SetFunctionSpaceActiveAsync(
        string hotelUnitCode,
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------------ Evenements ------------------------------------

    Task<IReadOnlyCollection<EventBookingResponse>> ListEventsAsync(
        string? hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        string? functionSpaceCode,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EventBookingResponse>> GetEventAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EventBookingResponse>> CreateEventAsync(
        CreateEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EventBookingResponse>> UpdateEventAsync(
        Guid id,
        UpdateEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Deplace l'evenement : rejoue le garde anti-double-reservation sur le nouveau creneau.</summary>
    Task<ApplicationResult<EventBookingResponse>> RescheduleEventAsync(
        Guid id,
        RescheduleEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EventBookingResponse>> ConfirmEventAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EventBookingResponse>> CancelEventAsync(
        Guid id,
        CancelEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------- Devis et BEO -------------------------------

    /// <summary>Remplace la totalite des lignes chiffrees. Refuse apres facturation.</summary>
    Task<ApplicationResult<EventBookingResponse>> ReplaceEventLinesAsync(
        Guid id,
        IReadOnlyCollection<EventBookingLineRequest> lines,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Remplace le deroule BEO. Reste possible APRES facturation : l'operation continue.</summary>
    Task<ApplicationResult<EventBookingResponse>> ReplaceEventScheduleAsync(
        Guid id,
        IReadOnlyCollection<EventScheduleItemRequest> schedule,
        OperationContext context,
        CancellationToken cancellationToken);

    // -------------------------- Facturation evenementielle --------------------------

    /// <summary>
    /// Genere la facture brouillon de l'evenement a partir de ses lignes, via le module
    /// Facturation qui en est proprietaire - et non par une seconde implementation : la facture
    /// d'un evenement doit etre exactement de la meme nature que toutes les autres.
    /// Idempotent par construction : un evenement deja facture est refuse.
    /// </summary>
    Task<ApplicationResult<EventBookingResponse>> InvoiceEventAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    // ==================== Allotements et rooming lists (volet GROUPES) ====================
    //
    // Un allotement retire des chambres de la vente publique SANS les nommer. La disponibilite les
    // soustrait et la creation de reservation refuse d'entamer le solde : les deux chemins
    // partagent le meme calcul dans LodgingService, faute de quoi l'hotel survendrait en silence.

    Task<IReadOnlyCollection<RoomAllotmentResponse>> ListAllotmentsAsync(
        string? hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeClosed,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomAllotmentResponse>> CreateAllotmentAsync(
        CreateRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Modifie le bloc. Le reduire en dessous de ce qui est deja pris est refuse.</summary>
    Task<ApplicationResult<RoomAllotmentResponse>> UpdateAllotmentAsync(
        Guid id,
        UpdateRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomAllotmentResponse>> ConfirmAllotmentAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rend le SOLDE a la vente avant terme. Les chambres deja prises sur le bloc restent
    /// reservees : liberer un allotement ne desengage personne.
    /// </summary>
    Task<ApplicationResult<RoomAllotmentResponse>> ReleaseAllotmentAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Annule le bloc. Refuse tant que des reservations y sont rattachees.</summary>
    Task<ApplicationResult<RoomAllotmentResponse>> CancelAllotmentAsync(
        Guid id,
        CancelRoomAllotmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomingListResponse>> GetRoomingListAsync(
        Guid allotmentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loge une liste d'occupants sur le bloc. Envoi PARTIEL assume : ce qui peut etre loge l'est,
    /// et ce qui ne l'est pas revient dans Rejected avec sa raison. Echouer en bloc sur un groupe
    /// de quarante personnes parce qu'un nom manque serait pire que loger les trente-neuf autres.
    /// </summary>
    Task<ApplicationResult<RoomingListResponse>> SubmitRoomingListAsync(
        Guid allotmentId,
        IReadOnlyCollection<RoomingListEntryRequest> entries,
        OperationContext context,
        CancellationToken cancellationToken);
}
