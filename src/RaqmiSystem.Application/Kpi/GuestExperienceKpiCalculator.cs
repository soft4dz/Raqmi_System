using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les indicateurs d'experience client tires des enquetes de satisfaction.
///
/// LE CLASSEMENT NPS N'EST PAS REFAIT ICI. Les bornes de la methode (0-6 detracteur, 7-8
/// passif, 9-10 promoteur) appartiennent au module CRM, qui les porte deja ; ce calculateur
/// l'appelle. Deux definitions du NPS dans un meme produit finiraient toujours par diverger, et
/// c'est precisement le genre de divergence qu'une bibliotheque KPI centralisee doit rendre
/// impossible.
///
/// LA CONSOLIDATION DU NPS. Le numerateur conserve est le solde (promoteurs - detracteurs)
/// EXPRIME EN POINTS POUR CENT REPONSES, et le denominateur le nombre de reponses. Ainsi la
/// valeur reste numerateur / denominateur, et le NPS d'un groupe se recalcule correctement en
/// additionnant les numerateurs et les denominateurs de ses unites - alors que moyenner les NPS
/// des unites donnerait le meme poids a un hotel qui a recu dix reponses et a un qui en a recu
/// mille.
/// </summary>
public sealed class GuestExperienceKpiCalculator
{
    private const string NoSurvey = "Aucune enquete de satisfaction sur la periode.";

    public IEnumerable<KpiMeasure> Compute(KpiPeriod period, string? unitCode, KpiFactSet facts)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);

        var surveys = facts.Satisfaction
            .Where(entry => entry.SurveyDate >= period.From && entry.SurveyDate <= period.To)
            .ToArray();

        yield return KpiMeasure.Ratio(
            KpiCodes.GuestSatisfactionScore,
            unitCode,
            surveys.Sum(entry => entry.Score),
            surveys.Length,
            KpiMath.Divide,
            NoSurvey);

        var promoters = surveys.Count(entry => SatisfactionEntry.Classify(entry.Score) == NpsCategory.Promoter);
        var detractors = surveys.Count(entry => SatisfactionEntry.Classify(entry.Score) == NpsCategory.Detractor);

        yield return KpiMeasure.Ratio(
            KpiCodes.Nps,
            unitCode,
            (promoters - detractors) * 100m,
            surveys.Length,
            KpiMath.Divide,
            NoSurvey);
    }
}
