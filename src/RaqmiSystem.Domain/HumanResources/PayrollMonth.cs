namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// A payroll period: one calendar month, the only granularity Algerian payroll works at
/// (declarations, CNAS deposits and the closing lock are all monthly).
///
/// Persisted as the ISO-like text "YYYY-MM" rather than a date, for two reasons that both
/// matter here: the text form is what CNAS/DADS-U exports and the desktop period picker
/// exchange, and its lexicographic order IS its chronological order, so "is this period before
/// the closed one" is a plain string comparison the database can index and answer without a
/// function call. A DateOnly pinned to the first of the month would have silently allowed a
/// mid-month value to mean something.
/// </summary>
public readonly record struct PayrollMonth : IComparable<PayrollMonth>
{
    public const int TextLength = 7;

    private PayrollMonth(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>First calendar day of the period - the lower bound of its time and absence data.</summary>
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>Last calendar day of the period - the upper bound, inclusive.</summary>
    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static PayrollMonth Parse(string value)
    {
        if (!TryParse(value, out var period))
        {
            throw new ArgumentException(
                "Payroll period must use the YYYY-MM format (for example 2026-08).",
                nameof(value));
        }

        return period;
    }

    public static bool TryParse(string? value, out PayrollMonth period)
    {
        period = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length != TextLength || trimmed[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(trimmed.AsSpan(0, 4), out var year)
            || !int.TryParse(trimmed.AsSpan(5, 2), out var month))
        {
            return false;
        }

        // The lower bound is not cosmetic: a year below 2000 is almost always a typo or a
        // mis-parsed date, and accepting it would let a payslip land in a period no closing
        // will ever cover.
        if (year is < 2000 or > 2999 || month is < 1 or > 12)
        {
            return false;
        }

        period = new PayrollMonth(year, month);
        return true;
    }

    public static PayrollMonth FromDate(DateOnly date)
    {
        return new PayrollMonth(date.Year, date.Month);
    }

    public PayrollMonth AddMonths(int count)
    {
        var shifted = FirstDay.AddMonths(count);
        return new PayrollMonth(shifted.Year, shifted.Month);
    }

    public bool Contains(DateOnly date)
    {
        return date.Year == Year && date.Month == Month;
    }

    public int CompareTo(PayrollMonth other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public static bool operator <(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PayrollMonth left, PayrollMonth right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        return $"{Year:D4}-{Month:D2}";
    }
}
