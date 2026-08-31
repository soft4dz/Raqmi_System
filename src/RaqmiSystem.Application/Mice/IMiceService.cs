using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Module 10.6 - Groupes et MICE, volet EVENEMENTIEL : espaces de reception, evenements, devis,
/// BEO et facturation evenementielle.
///
/// PERIMETRE ASSUME, ET IL EST PARTIEL. Le catalogue annonce six fonctions pour ce module ; celles
/// livrees ici sont les quatre qui portent sur les SALLES : espaces et evenements, devis, BEO,
/// facturation. Les deux autres - allotements et rooming lists - portent sur les CHAMBRES et ne
/// sont pas ici : un allotement retire des chambres de la vente, il doit donc etre soustrait a la
/// disponibilite ET au garde de creation de reservation, faute de quoi l'hotel survendrait
/// silencieusement. Cela se joue au coeur du PMS et merite sa propre passe.
///
/// Une salle de reception n'est PAS une chambre : elle se vend au creneau et non a la nuitee, elle
/// n'entre ni dans la disponibilite ni dans le taux d'occupation. C'est cette separation qui permet
/// a ce module d'exister sans toucher au coeur reservation.
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
}
