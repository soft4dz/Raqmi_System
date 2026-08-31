namespace RaqmiSystem.Application.Mice;

/// <summary>
/// La rooming list d'un bloc : le bloc lui-meme et les chambres deja attribuees.
/// <paramref name="Rejected"/> porte les lignes qu'un envoi n'a pas pu loger, avec la raison -
/// une soumission partielle doit dire ce qui est passe ET ce qui ne l'est pas, jamais echouer en
/// silence sur la moitie du groupe.
/// </summary>
public sealed record RoomingListResponse(
    RoomAllotmentResponse Allotment,
    IReadOnlyCollection<RoomingListEntryResponse> Entries,
    IReadOnlyCollection<string> Rejected);
