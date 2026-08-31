namespace RaqmiSystem.Domain.Crm;

/// <summary>Where a satisfaction answer was collected. Kept because response rate and score both vary a lot with it.</summary>
public enum SatisfactionSource
{
    /// <summary>Card or tablet left in the room.</summary>
    InRoom = 0,

    /// <summary>Questionnaire sent after the stay.</summary>
    Email = 1,

    /// <summary>Asked at the desk, typically at check-out.</summary>
    FrontDesk = 2,

    /// <summary>Answer collected online (site, booking platform, review).</summary>
    Online = 3,

    /// <summary>Answer collected during a call.</summary>
    Phone = 4
}
