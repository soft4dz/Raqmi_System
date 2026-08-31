namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// One line of the BEO timeline ("banquet event order"): the minute-by-minute running order the
/// operational departments work from - 08:00 room set, 10:30 coffee break, 12:30 lunch service.
///
/// It carries no price and no stock movement. The BEO says WHAT HAPPENS AND WHEN; the priced
/// lines say what is charged. Keeping them apart is what lets the kitchen read the running order
/// without seeing the client's commercial conditions.
/// </summary>
public sealed class EventScheduleItem
{
    public const int DescriptionMaxLength = 300;
    public const int DepartmentMaxLength = 60;

    private EventScheduleItem()
    {
    }

    public EventScheduleItem(TimeOnly startTime, string description, string? department = null)
    {
        StartTime = startTime;
        Description = RequireDescription(description);
        Department = NormalizeOptional(department, DepartmentMaxLength);
    }

    /// <summary>
    /// Self-assigned identifier: the EF configuration MUST declare ValueGeneratedNever(),
    /// otherwise EF marks a new item Modified instead of Added.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid EventBookingId { get; private set; }

    /// <summary>
    /// Wall-clock time of the hotel, not UTC. A running order is read by people standing in the
    /// building; converting it to UTC would help nobody and would break at the first time change.
    /// </summary>
    public TimeOnly StartTime { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Department expected to act (cuisine, technique, etage...), when identified.</summary>
    public string? Department { get; private set; }

    private static string RequireDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("The description is required.", nameof(description));
        }

        var trimmed = description.Trim();

        if (trimmed.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"The description cannot exceed {DescriptionMaxLength} characters.",
                nameof(description));
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
