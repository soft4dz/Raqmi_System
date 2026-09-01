using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un pointage journalier. Seules les heures VALIDEES comptent, exactement comme la pre-paie
/// les compte : des heures brutes non controlees ne doivent pas plus alimenter un indicateur de
/// productivite qu'un bulletin de paie.
/// </summary>
public sealed record KpiTimeEntryFact(
    string HotelUnitCode,
    string DepartmentCode,
    DateOnly WorkDate,
    decimal HoursWorked,
    TimeEntryStatus Status);
