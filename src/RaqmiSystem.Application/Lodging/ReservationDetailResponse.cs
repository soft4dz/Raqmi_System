namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// La vue complete d'un dossier : le dossier lui-meme, ses folios, ses extras, ses acomptes,
/// l'historique de ses chambres et son journal metier.
///
/// POURQUOI UNE SEULE REPONSE ET PAS SIX APPELS. Le comptoir ouvre ce dossier pendant que le client
/// attend ; six allers-retours, c'est six occasions d'afficher un ecran a moitie rempli. La lecture
/// est de toute facon faite dans la meme transaction, donc coherente.
/// </summary>
public sealed record ReservationDetailResponse(
    ReservationResponse Reservation,
    IReadOnlyCollection<FolioResponse> Folios,
    IReadOnlyCollection<ReservationExtraResponse> Extras,
    IReadOnlyCollection<DepositResponse> Deposits,
    IReadOnlyCollection<StayRoomAssignmentResponse> RoomHistory,
    IReadOnlyCollection<ReservationEventResponse> Journal,
    decimal TotalBalance);
