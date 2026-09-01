using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Regles d'exploitation hebergement d'UNE unite : heures de comptoir, tarification de l'arrivee
/// anticipee et du depart tardif, effet du hors service sur l'inventaire vendable, autorisation
/// generale de surreservation.
///
/// UNE LIGNE PAR UNITE, creee a la demande. Une unite sans ligne suit les valeurs par defaut
/// (<see cref="CreateDefault"/>) : 14h00 / 12h00, arrivee anticipee et depart tardif gratuits,
/// hors service NON deduit de l'inventaire commercial, surreservation interdite. Ces defauts sont
/// deliberement les plus prudents : ils ne vendent rien de plus et ne facturent rien de plus que
/// ce que l'hotel a explicitement decide.
///
/// POURQUOI CES REGLES NE SONT PAS DANS LE PLAN TARIFAIRE. Un plan tarifaire dit combien coute une
/// nuit ; ces regles disent a quelle heure la nuit commence et se termine, ce qui est une decision
/// de l'etablissement et non du produit vendu. Un hotel a une heure de check-in, pas une heure de
/// check-in par tarif.
/// </summary>
public sealed class LodgingPolicy : AuditableEntity
{
    /// <summary>Heure d'arrivee standard retenue a defaut de parametrage.</summary>
    public static readonly TimeOnly DefaultCheckInTime = new(14, 0);

    /// <summary>Heure de depart standard retenue a defaut de parametrage.</summary>
    public static readonly TimeOnly DefaultCheckOutTime = new(12, 0);

    private LodgingPolicy()
    {
    }

    public LodgingPolicy(string hotelUnitCode)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        CheckInTime = DefaultCheckInTime;
        CheckOutTime = DefaultCheckOutTime;
    }

    /// <summary>Politique par defaut d'une unite qui n'en a pas encore declare.</summary>
    public static LodgingPolicy CreateDefault(string hotelUnitCode)
    {
        return new LodgingPolicy(hotelUnitCode);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    // ---------------------------------- Heures de comptoir ----------------------------------

    /// <summary>Heure a partir de laquelle une chambre est mise a disposition sans supplement.</summary>
    public TimeOnly CheckInTime { get; private set; } = DefaultCheckInTime;

    /// <summary>Heure avant laquelle la chambre doit etre liberee sans supplement.</summary>
    public TimeOnly CheckOutTime { get; private set; } = DefaultCheckOutTime;

    // -------------------------------- Arrivee anticipee (ECI) --------------------------------

    /// <summary>
    /// Heure a partir de laquelle une arrivee anticipee est acceptee, quand elle est payante.
    /// Avant cette heure, la chambre n'est pas remise : la nuit precedente doit etre vendue.
    /// Null signifie qu'aucun plancher n'est pose et que toute arrivee anticipee est acceptable.
    /// </summary>
    public TimeOnly? EarlyCheckInFromTime { get; private set; }

    /// <summary>Vrai quand l'arrivee anticipee est offerte : aucun supplement n'est propose.</summary>
    public bool EarlyCheckInIsFree { get; private set; } = true;

    /// <summary>Supplement forfaitaire d'arrivee anticipee.</summary>
    public decimal EarlyCheckInFlatCharge { get; private set; }

    /// <summary>Supplement d'arrivee anticipee exprime en % du tarif de la nuit d'arrivee.</summary>
    public decimal EarlyCheckInPercentOfNight { get; private set; }

    // --------------------------------- Depart tardif (LCO) ---------------------------------

    /// <summary>
    /// Heure limite du depart tardif. Au-dela, ce n'est plus un depart tardif mais une nuit
    /// supplementaire, et le systeme le refuse plutot que de facturer une fraction de nuit qui
    /// empecherait la chambre d'etre revendue le soir meme.
    /// </summary>
    public TimeOnly? LateCheckOutUntilTime { get; private set; }

    /// <summary>Vrai quand le depart tardif est offert.</summary>
    public bool LateCheckOutIsFree { get; private set; } = true;

    /// <summary>Supplement forfaitaire de depart tardif.</summary>
    public decimal LateCheckOutFlatCharge { get; private set; }

    /// <summary>Supplement de depart tardif exprime en % du tarif de la derniere nuit.</summary>
    public decimal LateCheckOutPercentOfNight { get; private set; }

    // ------------------------------------- Inventaire -------------------------------------

    /// <summary>
    /// Le hors service d'exploitation (<see cref="RoomBlockKind.OutOfService"/>) retire-t-il la
    /// chambre de l'inventaire COMMERCIAL ?
    ///
    /// Faux par defaut, et ce defaut est un choix : un hotel qui bloque une chambre pour un usage
    /// interne accepte souvent de la reprendre si un client se presente. Le mettre a vrai est plus
    /// prudent commercialement mais reduit l'inventaire affiche. Le hors service TECHNIQUE
    /// (<see cref="RoomBlockKind.OutOfOrder"/>) n'est jamais concerne : il retire toujours.
    /// </summary>
    public bool OutOfServiceReducesInventory { get; private set; }

    /// <summary>
    /// Interrupteur general de la surreservation. Faux, aucune autorisation de surreservation ne
    /// s'applique, quelles que soient les lignes saisies : c'est le geste qui permet de couper la
    /// surreservation d'un coup en periode tendue sans effacer le parametrage.
    /// </summary>
    public bool OverbookingEnabled { get; private set; }

    // --------------------------------------- Gestes ---------------------------------------

    public void SetCounterHours(TimeOnly checkInTime, TimeOnly checkOutTime)
    {
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
    }

    public void SetEarlyCheckIn(
        TimeOnly? fromTime,
        bool isFree,
        decimal flatCharge,
        decimal percentOfNight)
    {
        EarlyCheckInFromTime = fromTime;
        EarlyCheckInIsFree = isFree;
        EarlyCheckInFlatCharge = LodgingText.Money(flatCharge, nameof(flatCharge));
        EarlyCheckInPercentOfNight = LodgingText.Percent(percentOfNight, nameof(percentOfNight));
    }

    public void SetLateCheckOut(
        TimeOnly? untilTime,
        bool isFree,
        decimal flatCharge,
        decimal percentOfNight)
    {
        LateCheckOutUntilTime = untilTime;
        LateCheckOutIsFree = isFree;
        LateCheckOutFlatCharge = LodgingText.Money(flatCharge, nameof(flatCharge));
        LateCheckOutPercentOfNight = LodgingText.Percent(percentOfNight, nameof(percentOfNight));
    }

    public void SetInventoryRules(bool outOfServiceReducesInventory, bool overbookingEnabled)
    {
        OutOfServiceReducesInventory = outOfServiceReducesInventory;
        OverbookingEnabled = overbookingEnabled;
    }

    /// <summary>
    /// Supplement d'arrivee anticipee pour une nuit facturee <paramref name="nightlyRate"/>.
    /// Zero quand l'arrivee anticipee est offerte. Le forfait et le pourcentage s'additionnent :
    /// un hotel qui n'en veut qu'un laisse l'autre a zero.
    /// </summary>
    public decimal ComputeEarlyCheckInCharge(decimal nightlyRate)
    {
        if (EarlyCheckInIsFree)
        {
            return 0m;
        }

        return decimal.Round(
            EarlyCheckInFlatCharge + (nightlyRate * EarlyCheckInPercentOfNight / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>Supplement de depart tardif pour une nuit facturee <paramref name="nightlyRate"/>.</summary>
    public decimal ComputeLateCheckOutCharge(decimal nightlyRate)
    {
        if (LateCheckOutIsFree)
        {
            return 0m;
        }

        return decimal.Round(
            LateCheckOutFlatCharge + (nightlyRate * LateCheckOutPercentOfNight / 100m),
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Une arrivee a <paramref name="arrivalTime"/> est-elle anticipee ? Une heure non renseignee
    /// n'est pas une arrivee anticipee : on ne facture pas un supplement sur une inconnue.
    /// </summary>
    public bool IsEarlyCheckIn(TimeOnly? arrivalTime)
    {
        return arrivalTime is { } time && time < CheckInTime;
    }

    /// <summary>Un depart a <paramref name="departureTime"/> est-il tardif ?</summary>
    public bool IsLateCheckOut(TimeOnly? departureTime)
    {
        return departureTime is { } time && time > CheckOutTime;
    }
}
