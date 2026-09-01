namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Les regles d'exploitation hebergement d'une unite. <paramref name="IsDefault"/> vaut vrai quand
/// l'unite n'a jamais rien declare : l'ecran affiche alors les valeurs prudentes par defaut plutot
/// qu'un formulaire vide, et l'operateur sait qu'il regarde un defaut et non un choix.
/// </summary>
public sealed record LodgingPolicyResponse(
    string HotelUnitCode,
    bool IsDefault,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    TimeOnly? EarlyCheckInFromTime,
    bool EarlyCheckInIsFree,
    decimal EarlyCheckInFlatCharge,
    decimal EarlyCheckInPercentOfNight,
    TimeOnly? LateCheckOutUntilTime,
    bool LateCheckOutIsFree,
    decimal LateCheckOutFlatCharge,
    decimal LateCheckOutPercentOfNight,
    bool OutOfServiceReducesInventory,
    bool OverbookingEnabled);
