namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// The complete statutory parameter set used to compute one payslip: social-contribution rates,
/// employer payroll taxes, the IRG abatement and scale, the monthly hour reference and the SMIG
/// compliance threshold.
///
/// WHY THIS IS DATA AND NOT CONSTANTS: every value here is set by Algerian law and moves with the
/// finance act. Compiling them in would mean a code release each time a rate changes, and - worse -
/// would make it impossible to recompute an old period with the rates that actually applied to it.
/// The set is therefore persisted and versioned by effective period (see
/// <see cref="PayrollParameterSet"/>); the engine is a pure function of (facts, parameters).
///
/// The defaults shipped by <see cref="PayrollParameterSet.CreateStatutoryDefault"/> reflect the
/// rules the legacy Hotel Metrics Pro payroll engine implemented. They are a STARTING POINT to be
/// confirmed against the finance act in force before the first real payroll run, not an assertion
/// that they are current.
/// </summary>
public sealed record PayrollParameters
{
    public required decimal MonthlyReferenceHours { get; init; }

    public required decimal OvertimeMultiplier { get; init; }

    public required int ReferenceDaysPerMonth { get; init; }

    /// <summary>Employee CNAS contribution rate, withheld from gross (0.09 = 9%).</summary>
    public required decimal EmployeeSocialRate { get; init; }

    /// <summary>Employer CNAS contribution rate, borne by the employer (0.26 = 26%).</summary>
    public required decimal EmployerSocialRate { get; init; }

    public required decimal WorkAccidentRate { get; init; }

    public required decimal UnemploymentInsuranceRate { get; init; }

    public required decimal VocationalTrainingRate { get; init; }

    /// <summary>Flat monthly IRG abatement, before the per-child part.</summary>
    public required decimal IncomeTaxAbatement { get; init; }

    public required decimal IncomeTaxAbatementPerChild { get; init; }

    /// <summary>Statutory minimum monthly wage, used as a compliance alert threshold only.</summary>
    public required decimal MinimumWage { get; init; }

    /// <summary>
    /// The progressive IRG scale, ordered from the lowest bracket up. The last bracket carries a
    /// null upper bound (open-ended).
    /// </summary>
    public required IReadOnlyList<IncomeTaxBracket> IncomeTaxBrackets { get; init; }
}
