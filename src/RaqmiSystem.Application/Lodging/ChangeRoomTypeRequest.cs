namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Surclassement ou declassement. Le sens est deduit du RANG des deux types, jamais du libelle :
/// c'est l'echelle de gamme declaree au parametrage qui dit ce qui est une montee en gamme.
///
/// <paramref name="Chargeable"/> faux fige le prix vendu a l'origine : le client occupe une suite
/// mais paie sa double. C'est le geste commercial le plus courant, et c'est precisement pour qu'il
/// reste lisible dans les comptes que le type d'origine est conserve sur le dossier.
/// </summary>
public sealed record ChangeRoomTypeRequest(
    string RoomTypeCode,
    string Reason,
    bool Chargeable = false,
    Guid? TargetRoomId = null,
    bool AllowOverbooking = false);
