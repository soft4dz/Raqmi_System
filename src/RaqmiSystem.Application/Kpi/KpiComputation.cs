using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le resultat d'une passe de calcul : toutes les mesures d'une periode, pour le groupe et pour
/// chaque unite, indexees pour un acces direct.
///
/// La cle est le couple (code, unite), l'unite nulle designant le groupe. C'est ce qui permet au
/// service de rapprocher sans effort la mesure de la periode et celle de N-1, ou de construire
/// une ligne de comparatif inter-unites.
/// </summary>
public sealed class KpiComputation
{
    private readonly IReadOnlyDictionary<(string Code, string? Unit), KpiMeasure> index;

    public KpiComputation(
        KpiPeriod period,
        IReadOnlyCollection<KpiMeasure> measures,
        KpiCapacity groupCapacity)
    {
        Period = period;
        Measures = measures;
        GroupCapacity = groupCapacity;

        // Derniere mesure gagnante : un calculateur ne doit jamais emettre deux fois le meme
        // code sur le meme perimetre, et le test KpiEngineTests l'epingle. Le dictionnaire est
        // construit sans exception pour qu'une eventuelle duplication n'empeche pas l'ecran de
        // s'afficher - elle se voit dans les tests, pas devant l'utilisateur.
        var map = new Dictionary<(string, string?), KpiMeasure>();

        foreach (var measure in measures)
        {
            map[(measure.Code, measure.HotelUnitCode)] = measure;
        }

        index = map;
    }

    public KpiPeriod Period { get; }

    public IReadOnlyCollection<KpiMeasure> Measures { get; }

    /// <summary>Capacite consolidee du groupe, reutilisee par les indicateurs par chambre.</summary>
    public KpiCapacity GroupCapacity { get; }

    public KpiMeasure? Find(string code, string? hotelUnitCode)
    {
        return index.GetValueOrDefault((code, hotelUnitCode));
    }

    /// <summary>
    /// La mesure demandee, ou une mesure "sans objet" portant la raison - jamais null. Un ecran
    /// doit toujours pouvoir afficher une ligne, meme quand l'indicateur n'a pas de valeur : une
    /// case vide sans explication est ce que cette bibliotheque cherche precisement a eviter.
    /// </summary>
    public KpiMeasure Require(string code, string? hotelUnitCode)
    {
        return Find(code, hotelUnitCode)
            ?? KpiMeasure.NotApplicable(
                code,
                hotelUnitCode,
                "Cet indicateur n'a pas ete calcule sur ce perimetre.");
    }
}
