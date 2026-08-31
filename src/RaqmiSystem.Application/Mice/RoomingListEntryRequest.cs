namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Une ligne de rooming list : un occupant a loger sur le bloc.
/// Les dates sont facultatives et retombent sur celles du bloc, ce qui couvre le cas courant ou
/// tout le groupe arrive et repart ensemble.
/// </summary>
public sealed record RoomingListEntryRequest(
    string GuestName,
    int GuestCount,
    DateOnly? ArrivalDate,
    DateOnly? DepartureDate);
