using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les bornes applicables a un indicateur sur un perimetre donne, une fois la resolution faite.
///
/// LA RESOLUTION : une borne portant l'unite l'emporte sur la borne du groupe, entierement -
/// pas champ par champ. Melanger le seuil favorable de l'unite et le seuil critique du groupe
/// donnerait un couple de bornes que personne n'a valide ensemble et qui pourrait meme etre
/// incoherent, alors que le domaine verifie leur coherence a la saisie. Une regle d'unite est
/// donc une regle complete, ou elle n'est pas.
/// </summary>
public sealed record KpiThresholdSet(
    decimal? FavorableThreshold,
    decimal? CriticalThreshold,
    decimal? TargetValue,
    string? OwnerRole)
{
    /// <summary>Aucune borne : le moteur ne rend alors aucun verdict de sante.</summary>
    public static KpiThresholdSet None { get; } = new(null, null, null, null);

    public bool HasThreshold => FavorableThreshold is not null || CriticalThreshold is not null;

    /// <summary>
    /// La borne applicable a ce couple (indicateur, unite) parmi les seuils actifs configures :
    /// la regle de l'unite si elle existe, sinon celle du groupe, sinon aucune.
    /// </summary>
    public static KpiThresholdSet Resolve(
        string kpiCode,
        string? hotelUnitCode,
        IReadOnlyCollection<KpiThreshold> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        var candidates = thresholds
            .Where(threshold => threshold.IsActive
                && string.Equals(threshold.KpiCode, kpiCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var match = candidates.FirstOrDefault(threshold =>
                hotelUnitCode is not null
                && string.Equals(threshold.HotelUnitCode, hotelUnitCode, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(threshold => threshold.HotelUnitCode is null);

        return match is null
            ? None
            : new KpiThresholdSet(
                match.FavorableThreshold,
                match.CriticalThreshold,
                match.TargetValue,
                match.OwnerRole);
    }
}
