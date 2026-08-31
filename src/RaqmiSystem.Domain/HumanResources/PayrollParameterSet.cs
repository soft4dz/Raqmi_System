using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// A statutory payroll parameter set, versioned by the period it takes effect from. The payroll
/// run resolves the set to use as "the most recent set whose EffectiveFrom is at or before the
/// period being computed", so recomputing an old month applies the rates that were in force
/// THEN, not the current ones.
///
/// This is the answer to the single biggest maintenance flaw of the legacy payroll engine, where
/// the CNAS rates, the IRG abatement, the scale and the SMIG were compiled-in constants: a
/// finance act meant a code release, and an old period could no longer be reproduced once the
/// constants had moved. Here a rate change is a new row, entered by an authorised user and
/// audited like any other sensitive write.
/// </summary>
public sealed class PayrollParameterSet : AuditableEntity
{
    private readonly List<PayrollTaxBracket> _brackets = new();

    private PayrollParameterSet()
    {
    }

    public PayrollParameterSet(
        PayrollMonth effectiveFrom,
        string label,
        decimal monthlyReferenceHours,
        decimal overtimeMultiplier,
        int referenceDaysPerMonth,
        decimal employeeSocialRate,
        decimal employerSocialRate,
        decimal workAccidentRate,
        decimal unemploymentInsuranceRate,
        decimal vocationalTrainingRate,
        decimal incomeTaxAbatement,
        decimal incomeTaxAbatementPerChild,
        decimal minimumWage)
    {
        EffectiveFrom = effectiveFrom;
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        MonthlyReferenceHours = RequirePositive(monthlyReferenceHours, nameof(monthlyReferenceHours));
        OvertimeMultiplier = RequireMultiplier(overtimeMultiplier);
        ReferenceDaysPerMonth = RequireReferenceDays(referenceDaysPerMonth);
        EmployeeSocialRate = RequireRate(employeeSocialRate, nameof(employeeSocialRate));
        EmployerSocialRate = RequireRate(employerSocialRate, nameof(employerSocialRate));
        WorkAccidentRate = RequireRate(workAccidentRate, nameof(workAccidentRate));
        UnemploymentInsuranceRate = RequireRate(unemploymentInsuranceRate, nameof(unemploymentInsuranceRate));
        VocationalTrainingRate = RequireRate(vocationalTrainingRate, nameof(vocationalTrainingRate));
        IncomeTaxAbatement = RequireNotNegative(incomeTaxAbatement, nameof(incomeTaxAbatement));
        IncomeTaxAbatementPerChild = RequireNotNegative(
            incomeTaxAbatementPerChild,
            nameof(incomeTaxAbatementPerChild));
        MinimumWage = RequireNotNegative(minimumWage, nameof(minimumWage));
    }

    public PayrollMonth EffectiveFrom { get; private set; }

    /// <summary>Human label of the version, typically the finance act it comes from.</summary>
    public string Label { get; private set; } = string.Empty;

    public decimal MonthlyReferenceHours { get; private set; }

    public decimal OvertimeMultiplier { get; private set; }

    public int ReferenceDaysPerMonth { get; private set; }

    public decimal EmployeeSocialRate { get; private set; }

    public decimal EmployerSocialRate { get; private set; }

    public decimal WorkAccidentRate { get; private set; }

    public decimal UnemploymentInsuranceRate { get; private set; }

    public decimal VocationalTrainingRate { get; private set; }

    public decimal IncomeTaxAbatement { get; private set; }

    public decimal IncomeTaxAbatementPerChild { get; private set; }

    public decimal MinimumWage { get; private set; }

    public IReadOnlyList<PayrollTaxBracket> Brackets => _brackets.AsReadOnly();

    /// <summary>
    /// The parameter set shipped with the module, reproducing the rules the legacy Hotel Metrics
    /// Pro payroll engine implemented: 173.33 monthly reference hours (40h week), overtime at
    /// +50%, unpaid days valued on a 30-day month, CNAS 9% employee and 26% employer, employer
    /// payroll taxes of 1.25% work accident, 1.5% unemployment insurance and 1% vocational
    /// training, an IRG abatement of 40 000 DZD plus 1 000 per dependent child, a 23/27/33
    /// progressive scale and a 20 000 DZD minimum wage.
    ///
    /// MUST BE CONFIRMED against the finance act in force before the first real payroll run. It
    /// is a faithful port of what the previous system computed, which is not the same claim as
    /// being current - and the whole point of this entity is that confirming it is a data edit,
    /// not a code change.
    /// </summary>
    public static PayrollParameterSet CreateStatutoryDefault(PayrollMonth effectiveFrom, string label)
    {
        var set = new PayrollParameterSet(
            effectiveFrom,
            label,
            monthlyReferenceHours: 173.33m,
            overtimeMultiplier: 1.5m,
            referenceDaysPerMonth: 30,
            employeeSocialRate: 0.09m,
            employerSocialRate: 0.26m,
            workAccidentRate: 0.0125m,
            unemploymentInsuranceRate: 0.015m,
            vocationalTrainingRate: 0.01m,
            incomeTaxAbatement: 40_000m,
            incomeTaxAbatementPerChild: 1_000m,
            minimumWage: 20_000m);

        set.ReplaceBrackets(new[]
        {
            new IncomeTaxBracket(30_000m, 0.23m),
            new IncomeTaxBracket(120_000m, 0.27m),
            new IncomeTaxBracket(null, 0.33m)
        });

        return set;
    }

    /// <summary>
    /// Replaces the whole scale at once. Brackets are validated as a SET rather than one by one:
    /// ascending bounds, no duplicate bound, and exactly one open-ended bracket which must be
    /// last. A scale with a gap, an inversion or two open ends does not merely look wrong - it
    /// silently taxes the wrong fraction of income for every employee.
    /// </summary>
    public void ReplaceBrackets(IReadOnlyList<IncomeTaxBracket> brackets)
    {
        ArgumentNullException.ThrowIfNull(brackets);

        if (brackets.Count == 0)
        {
            throw new ArgumentException("At least one income tax bracket is required.", nameof(brackets));
        }

        decimal? previousBound = null;

        for (var index = 0; index < brackets.Count; index++)
        {
            var bracket = brackets[index];
            var isLast = index == brackets.Count - 1;

            if (bracket.UpperBound is null && !isLast)
            {
                throw new ArgumentException(
                    "Only the last income tax bracket may be open-ended.",
                    nameof(brackets));
            }

            if (bracket.UpperBound is not null)
            {
                if (bracket.UpperBound <= 0m)
                {
                    throw new ArgumentException(
                        "An income tax bracket upper bound must be greater than zero.",
                        nameof(brackets));
                }

                if (previousBound is not null && bracket.UpperBound <= previousBound)
                {
                    throw new ArgumentException(
                        "Income tax bracket upper bounds must be strictly increasing.",
                        nameof(brackets));
                }

                previousBound = bracket.UpperBound;
            }

            if (bracket.Rate is < 0m or > 1m)
            {
                throw new ArgumentException(
                    "An income tax rate must be a fraction between 0 and 1.",
                    nameof(brackets));
            }
        }

        _brackets.Clear();

        for (var index = 0; index < brackets.Count; index++)
        {
            _brackets.Add(new PayrollTaxBracket(index, brackets[index].UpperBound, brackets[index].Rate));
        }
    }

    public void UpdateLabel(string label)
    {
        Label = HumanResourcesText.Require(label, nameof(label), 160);
    }

    /// <summary>Projects the set into the immutable input the calculation engine consumes.</summary>
    public PayrollParameters ToParameters()
    {
        if (_brackets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Payroll parameter set effective from {EffectiveFrom} carries no income tax bracket.");
        }

        return new PayrollParameters
        {
            MonthlyReferenceHours = MonthlyReferenceHours,
            OvertimeMultiplier = OvertimeMultiplier,
            ReferenceDaysPerMonth = ReferenceDaysPerMonth,
            EmployeeSocialRate = EmployeeSocialRate,
            EmployerSocialRate = EmployerSocialRate,
            WorkAccidentRate = WorkAccidentRate,
            UnemploymentInsuranceRate = UnemploymentInsuranceRate,
            VocationalTrainingRate = VocationalTrainingRate,
            IncomeTaxAbatement = IncomeTaxAbatement,
            IncomeTaxAbatementPerChild = IncomeTaxAbatementPerChild,
            MinimumWage = MinimumWage,
            IncomeTaxBrackets = _brackets
                .OrderBy(bracket => bracket.Ordinal)
                .Select(bracket => bracket.ToBracket())
                .ToArray()
        };
    }

    private static decimal RequirePositive(decimal value, string argumentName)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Value must be greater than zero.");
        }

        return value;
    }

    private static decimal RequireNotNegative(decimal value, string argumentName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Value cannot be negative.");
        }

        return value;
    }

    private static decimal RequireRate(decimal value, string argumentName)
    {
        if (value is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                argumentName,
                "A contribution rate must be a fraction between 0 and 1.");
        }

        return value;
    }

    private static decimal RequireMultiplier(decimal value)
    {
        if (value < 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The overtime multiplier cannot be below 1.");
        }

        return value;
    }

    private static int RequireReferenceDays(int value)
    {
        if (value is < 28 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The monthly reference day count must be between 28 and 31.");
        }

        return value;
    }
}
