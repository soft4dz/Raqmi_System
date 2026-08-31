namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// Lifecycle of an event booking.
///
/// The decisive rule is that <see cref="Draft"/> ALREADY HOLDS the space, exactly like
/// <see cref="Confirmed"/>. That is deliberate and matches how banqueting actually works: a quote
/// sent to a client is an option on the room, and a venue that let two salespeople quote the same
/// Saturday evening would sell the same ballroom twice. Only <see cref="Cancelled"/> releases it.
/// </summary>
public enum EventBookingStatus
{
    /// <summary>Quote sent or being prepared. Holds the space (option).</summary>
    Draft = 0,

    /// <summary>Client agreed. Holds the space and can be invoiced.</summary>
    Confirmed = 1,

    /// <summary>Cancelled: releases the space. A reason is mandatory.</summary>
    Cancelled = 2
}
