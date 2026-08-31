namespace RaqmiSystem.Domain.Crm;

/// <summary>How a contact with a guest happened. Feeds the relationship timeline of the 360 view.</summary>
public enum InteractionChannel
{
    Phone = 0,
    Email = 1,
    Sms = 2,

    /// <summary>Face to face: front desk, meeting, visit.</summary>
    InPerson = 3,

    /// <summary>Web form, chat, booking platform message.</summary>
    Web = 4
}
