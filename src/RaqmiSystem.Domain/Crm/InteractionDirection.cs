namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// Who started the contact. Kept because it changes what the timeline means: a run of inbound
/// calls is a guest chasing the establishment, the same run outbound is the establishment doing
/// its job.
/// </summary>
public enum InteractionDirection
{
    /// <summary>The guest contacted the establishment.</summary>
    Inbound = 0,

    /// <summary>The establishment contacted the guest.</summary>
    Outbound = 1
}
