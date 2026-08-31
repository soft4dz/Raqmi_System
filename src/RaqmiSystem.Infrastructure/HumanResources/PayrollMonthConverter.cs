using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Infrastructure.HumanResources;

/// <summary>
/// Stores a <see cref="PayrollMonth"/> as its "YYYY-MM" text. Text rather than a date because
/// the lexicographic order of that format is the chronological order, so range filters and
/// ORDER BY on a payroll period work directly on the column, with no function call and no index
/// left unused.
/// </summary>
public sealed class PayrollMonthConverter : ValueConverter<PayrollMonth, string>
{
    public PayrollMonthConverter()
        : base(period => period.ToString(), value => PayrollMonth.Parse(value))
    {
    }
}
