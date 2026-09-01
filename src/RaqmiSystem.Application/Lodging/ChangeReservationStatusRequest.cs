using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Passage d'un statut d'avant-arrivee a un autre : demande, option, confirmee, garantie.
/// </summary>
public sealed record ChangeReservationStatusRequest(ReservationStatus Status, string? Reason = null);
