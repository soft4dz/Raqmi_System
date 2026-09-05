using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Crm;

/// <summary>
/// L'historique des séjours lu dans les réservations du PMS. SEUL point de contact entre le CRM
/// et <c>RaqmiSystem.Domain.Lodging</c> : si l'hébergement sort d'une installation, ce fichier
/// sort avec lui et le CRM n'en sait rien (voir <see cref="NoStayHistoryReader"/>).
/// </summary>
public sealed class LodgingStayHistoryReader(RaqmiDbContext dbContext) : IStayHistoryReader
{
    public Task<bool> StayExistsAsync(Guid stayId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Reservation>()
            .AnyAsync(reservation => reservation.Id == stayId, cancellationToken);
    }

    public async Task<GuestStayStatistics> ReadAsync(
        string customerCode,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Le total du séjour est lu dans les tarifs par nuit figés sur chaque réservation
        // (Reservation.TotalStayAmount), qu'EF ne mappe pas : les lignes sont matérialisées. Un
        // client a une poignée de séjours - ce n'est pas un rapport sur tout l'hôtel.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.CustomerCode == customerCode)
            .ToArrayAsync(cancellationToken);

        // Ce que le client a réellement dormi : une réservation à venir n'est pas un séjour, et
        // une annulée ne l'a jamais été.
        var stayed = reservations
            .Where(reservation => reservation.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
            .ToArray();

        return new GuestStayStatistics(
            stayed.Length,
            stayed.Sum(reservation => reservation.Nights),
            stayed.Length == 0 ? null : stayed.Min(reservation => reservation.ArrivalDate),
            stayed.Length == 0 ? null : stayed.Max(reservation => reservation.DepartureDate),
            stayed.Sum(reservation => reservation.TotalStayAmount),
            reservations.Count(reservation =>
                reservation.Status.IsPreArrival() && reservation.ArrivalDate >= today),
            reservations.Count(reservation => reservation.Status == ReservationStatus.Cancelled),
            reservations.Count(reservation => reservation.Status == ReservationStatus.NoShow));
    }
}
