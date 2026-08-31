using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// One recorded contact with a guest: the line of the relationship timeline shown by the 360
/// view. Interactions are written once and never edited - the timeline is a record of what
/// happened, not a working document, and a note rewritten after the fact would make the history
/// useless exactly when someone needs it (a dispute, a handover between shifts).
///
/// <see cref="HandledBy"/> is the name of whoever dealt with the guest, which is not always the
/// account that typed it in - a receptionist records the call a colleague took. It is therefore
/// free text, captured next to the audit trail's own actor rather than instead of it.
/// </summary>
public sealed class GuestInteraction : AuditableEntity
{
    private GuestInteraction()
    {
    }

    public GuestInteraction(
        string customerCode,
        DateTimeOffset occurredAt,
        InteractionChannel channel,
        InteractionDirection direction,
        string subject,
        string handledBy,
        string? hotelUnitCode = null,
        string? notes = null)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown interaction channel.");
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown interaction direction.");
        }

        CustomerCode = Customer.NormalizeCode(customerCode);
        HotelUnitCode = string.IsNullOrWhiteSpace(hotelUnitCode) ? null : HotelUnit.NormalizeCode(hotelUnitCode);
        OccurredAt = occurredAt;
        Channel = channel;
        Direction = direction;
        Subject = CrmText.Require(subject, nameof(subject), 200);
        HandledBy = CrmText.Require(handledBy, nameof(handledBy), 160);
        Notes = CrmText.Optional(notes, nameof(notes), 2000);
    }

    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>Unit concerned, when the contact was about one. Null for a group-level exchange.</summary>
    public string? HotelUnitCode { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public InteractionChannel Channel { get; private set; }

    public InteractionDirection Direction { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    /// <summary>Name of the person who dealt with the guest.</summary>
    public string HandledBy { get; private set; } = string.Empty;

    public string? Notes { get; private set; }
}
