using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// The guest's account for one stay, opened automatically at check-in (exactly one folio per
/// reservation, guarded by a unique index on reservation_id). The balance is nothing but the sum
/// of its lines: nights and extras push it up, settlements and negative adjustments bring it
/// back down, and check-out is refused while it is not exactly zero.
/// </summary>
public sealed class Folio : AuditableEntity
{
    private readonly List<FolioCharge> _charges = new();

    private Folio()
    {
    }

    public Folio(Guid reservationId)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("Reservation id is required.", nameof(reservationId));
        }

        ReservationId = reservationId;
    }

    public Guid ReservationId { get; private set; }

    public IReadOnlyCollection<FolioCharge> Charges => _charges.AsReadOnly();

    public decimal Balance => _charges.Sum(charge => charge.Amount);

    public void AddCharge(FolioCharge charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        charge.SetLineNumber(_charges.Count + 1);
        _charges.Add(charge);
    }
}
