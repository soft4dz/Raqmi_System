namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un collaborateur, reduit a son affectation et a ses bornes de presence. Ces deux dates
/// suffisent a repondre aux trois questions des indicateurs RH : etait-il present a telle date
/// (effectif), est-il parti dans la periode (turnover), combien de jours a-t-il ete
/// contractuellement present (absenteisme).
/// </summary>
public sealed record KpiEmployeeFact(
    Guid EmployeeId,
    string HotelUnitCode,
    string DepartmentCode,
    DateOnly HireDate,
    DateOnly? TerminationDate)
{
    /// <summary>Present a cette date : embauche au plus tard ce jour-la, et pas encore parti.</summary>
    public bool IsPresentOn(DateOnly date)
    {
        return HireDate <= date && (TerminationDate is null || TerminationDate.Value >= date);
    }
}
