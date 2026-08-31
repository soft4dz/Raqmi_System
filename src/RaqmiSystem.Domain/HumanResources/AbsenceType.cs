namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Absence nature. The distinction that matters to payroll is carried by
/// <see cref="AbsenceTypeExtensions.IsUnpaid"/>: only unpaid days are deducted from the gross.
/// </summary>
public enum AbsenceType
{
    AnnualLeave = 0,
    SickLeave = 1,

    /// <summary>Unpaid leave - the only type deducted from the salary.</summary>
    UnpaidLeave = 2,

    Maternity = 3,
    Exceptional = 4
}

public static class AbsenceTypeExtensions
{
    /// <summary>
    /// True when days of this type are deducted from the gross salary. Sick leave and maternity
    /// are compensated by CNAS rather than deducted here, so they are NOT unpaid for payroll
    /// purposes even though the employee is away.
    /// </summary>
    public static bool IsUnpaid(this AbsenceType type)
    {
        return type == AbsenceType.UnpaidLeave;
    }
}
