using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Fabrique de reservations pour les tests.
///
/// POURQUOI ELLE EXISTE. Depuis que le PMS vend PAR TYPE, une reservation porte deux informations
/// que le constructeur exige et que la plupart des tests n'ont aucune raison de choisir : un numero
/// de dossier unique dans l'unite, et le code du type vendu. Les faire figurer dans chaque test
/// noierait ce que chaque test cherche reellement a montrer. La fabrique les fournit, et n'expose
/// que ce qui compte pour l'assertion.
///
/// Le numero est tire d'un compteur : il doit etre unique dans l'unite, sinon l'index
/// ux_reservations_hotel_unit_code_number ferait echouer l'insertion pour une raison sans rapport
/// avec le test.
/// </summary>
internal static class TestReservations
{
    private static int sequence;

    /// <summary>Un dossier confirme, vendu sur <paramref name="roomTypeCode"/>.</summary>
    public static Reservation Create(
        string hotelUnitCode,
        Guid? roomId,
        string customerCode,
        DateOnly arrivalDate,
        DateOnly departureDate,
        int guests,
        decimal nightlyRate,
        string ratePlanCode,
        string roomTypeCode = "DBL")
    {
        return new Reservation(
            hotelUnitCode,
            NextNumber(),
            roomTypeCode,
            roomId,
            customerCode,
            arrivalDate,
            departureDate,
            guests,
            nightlyRate,
            ratePlanCode);
    }

    /// <summary>Un numero de dossier unique, au format que le service produit.</summary>
    public static string NextNumber()
    {
        return $"T{Interlocked.Increment(ref sequence):D8}";
    }
}
