using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// A bookable meeting or banqueting space of one hotel unit: ballroom, meeting room, terrace.
///
/// It is deliberately NOT a <see cref="Lodging.Room"/>. A function space is sold by the time slot
/// and not by the night, it never appears in room availability, and it never enters occupancy
/// statistics. Keeping the two apart is what allows this whole module to exist without touching
/// the reservation core of the PMS.
/// </summary>
public sealed class FunctionSpace : AuditableEntity
{
    public const int CodeMaxLength = 16;
    public const int LabelMaxLength = 120;
    public const int NotesMaxLength = 500;

    /// <summary>A room seating more than this is a typo, not a venue.</summary>
    public const int MaxCapacity = 5_000;

    private FunctionSpace()
    {
    }

    public FunctionSpace(
        string hotelUnitCode,
        string code,
        string label,
        int maxCapacity,
        decimal? areaSquareMeters = null,
        string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = NormalizeCode(code);
        Label = RequireText(label, nameof(label), LabelMaxLength);
        MaxAttendance = RequireCapacity(maxCapacity);
        AreaSquareMeters = RequireArea(areaSquareMeters);
        Notes = NormalizeOptional(notes, NotesMaxLength);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Short code, unique WITHIN the unit (two hotels may both have a "SALLE1").</summary>
    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>
    /// Largest number of people the space can take, all layouts considered. See
    /// <see cref="EventSetupStyle"/> for why a single figure is a stated simplification.
    /// </summary>
    public int MaxAttendance { get; private set; }

    /// <summary>Floor area, when known. Purely descriptive; nothing is computed from it.</summary>
    public decimal? AreaSquareMeters { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// A deactivated space stops being offered for NEW bookings. Events already placed in it are
    /// left untouched: cancelling somebody's booked wedding because a space was archived would be
    /// a far worse outcome than an inactive space still showing an old event.
    /// </summary>
    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, int maxCapacity, decimal? areaSquareMeters, string? notes)
    {
        Label = RequireText(label, nameof(label), LabelMaxLength);
        MaxAttendance = RequireCapacity(maxCapacity);
        AreaSquareMeters = RequireArea(areaSquareMeters);
        Notes = NormalizeOptional(notes, NotesMaxLength);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public static string NormalizeCode(string code)
    {
        var normalized = RequireText(code, nameof(code), CodeMaxLength).ToUpperInvariant();

        return normalized;
    }

    private static int RequireCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be strictly positive.");
        }

        if (capacity > MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                $"Capacity cannot exceed {MaxCapacity}.");
        }

        return capacity;
    }

    private static decimal? RequireArea(decimal? area)
    {
        if (area is null)
        {
            return null;
        }

        if (area <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(area), "Area must be strictly positive when provided.");
        }

        return decimal.Round(area.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value is required.", parameterName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", parameterName);
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
