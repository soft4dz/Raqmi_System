using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// La fenetre d'analyse : deux bornes incluses et le grain qu'elles representent. Le grain ne
/// change aucun calcul - le moteur ne connait que [du, au] - il sert a nommer la periode dans
/// l'historique et a empecher qu'une courbe melange des points mensuels et trimestriels.
/// </summary>
public sealed record KpiPeriod(DateOnly From, DateOnly To, KpiPeriodGranularity Granularity)
{
    /// <summary>Nombre de jours couverts, bornes incluses. Toujours au moins 1.</summary>
    public int DayCount => To.DayNumber - From.DayNumber + 1;

    /// <summary>La periode equivalente un an plus tot, pour la comparaison N/N-1.</summary>
    public KpiPeriod PreviousYear()
    {
        var (from, to) = KpiMath.PreviousYearPeriod(From, To);
        return new KpiPeriod(from, to, Granularity);
    }

    /// <summary>
    /// Deduit le grain de bornes libres : un jour, une semaine, un mois entier, un trimestre
    /// entier, une annee entiere, ou <see cref="KpiPeriodGranularity.Custom"/> sinon. La
    /// deduction est STRICTE - un mois amput d'un jour est une periode libre, pas un mois -
    /// parce qu'un historique mensuel qui accueillerait des mois incomplets ne serait plus
    /// comparable d'une annee sur l'autre.
    /// </summary>
    public static KpiPeriod Create(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("La fin de periode ne peut pas preceder son debut.", nameof(to));
        }

        return new KpiPeriod(from, to, DetectGranularity(from, to));
    }

    /// <summary>Le mois calendaire complet contenant cette date.</summary>
    public static KpiPeriod Month(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return new KpiPeriod(first, first.AddMonths(1).AddDays(-1), KpiPeriodGranularity.Month);
    }

    private static KpiPeriodGranularity DetectGranularity(DateOnly from, DateOnly to)
    {
        if (from == to)
        {
            return KpiPeriodGranularity.Day;
        }

        if (from.Day == 1)
        {
            if (to == from.AddMonths(1).AddDays(-1))
            {
                return KpiPeriodGranularity.Month;
            }

            if (from.Month is 1 or 4 or 7 or 10 && to == from.AddMonths(3).AddDays(-1))
            {
                return KpiPeriodGranularity.Quarter;
            }

            if (from.Month == 1 && to == from.AddYears(1).AddDays(-1))
            {
                return KpiPeriodGranularity.Year;
            }
        }

        if (to.DayNumber - from.DayNumber + 1 == 7)
        {
            return KpiPeriodGranularity.Week;
        }

        return KpiPeriodGranularity.Custom;
    }
}
