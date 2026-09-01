using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une absence declaree. Seules les absences APPROUVEES comptent : une demande en attente n'est
/// pas encore une absence, et une demande refusee n'en a jamais ete une. Le statut est
/// transporte pour que le calculateur applique lui-meme cette regle.
///
/// Les bornes sont incluses des deux cotes, comme dans le module RH.
/// </summary>
public sealed record KpiAbsenceFact(
    Guid EmployeeId,
    string HotelUnitCode,
    string DepartmentCode,
    AbsenceType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    AbsenceStatus Status)
{
    /// <summary>Nombre de jours de cette absence tombant dans la fenetre demandee.</summary>
    public int DaysWithin(DateOnly from, DateOnly to)
    {
        var start = StartDate > from ? StartDate : from;
        var end = EndDate < to ? EndDate : to;

        return end < start ? 0 : end.DayNumber - start.DayNumber + 1;
    }
}
