namespace RaqmiSystem.Domain.Receivables;

/// <summary>
/// How the dunning action was carried out. This records what a human being did outside the
/// system: the application never sends anything by itself.
/// </summary>
public enum ReminderChannel
{
    Phone,
    Email,
    Letter,
    InPerson
}
