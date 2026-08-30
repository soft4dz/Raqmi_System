namespace RaqmiSystem.Domain.Lodging;

public enum ReservationStatus
{
    /// <summary>Reserved, the guest has not arrived yet.</summary>
    Booked,

    /// <summary>The guest is in the room; the folio is open.</summary>
    CheckedIn,

    /// <summary>The stay is over and the folio was settled to zero.</summary>
    CheckedOut,

    /// <summary>Cancelled before arrival (mandatory reason). Does not block the room.</summary>
    Cancelled,

    /// <summary>The guest never showed up (only after the arrival date). Does not block the room.</summary>
    NoShow
}
