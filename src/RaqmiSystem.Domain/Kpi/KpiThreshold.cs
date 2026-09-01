using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Les bornes de pilotage d'un indicateur, telles que l'etablissement les a fixees. C'est la
/// seule donnee du moteur KPI qui soit saisie par un humain : tout le reste est calcule.
///
/// DEUX BORNES, TROIS ETATS. Le vocabulaire de gestion parle de trois seuils - favorable,
/// vigilance, critique - mais trois bornes decouperaient QUATRE bandes pour trois etats, et la
/// quatrieme serait indefinissable. La vigilance n'est donc pas une borne : c'est la bande
/// ENTRE les deux autres. Un taux d'occupation favorable a partir de 65 % et critique a 40 % ou
/// moins est en vigilance entre les deux, sans qu'aucun troisieme nombre ait a etre saisi. Les
/// bornes sont inclusives (voir <see cref="KpiMath.Classify"/>) : un seuil qu'on peut atteindre
/// sans consequence n'est pas un seuil.
///
/// PORTEE. Une borne sans unite (<see cref="HotelUnitCode"/> nul) est la regle du groupe ; une
/// borne portant une unite ne vaut que pour elle et prend le pas sur la regle groupe. C'est ce
/// qui permet a un resort saisonnier et a un hotel d'affaires de partager le meme catalogue
/// sans partager les memes attentes.
///
/// L'OBJECTIF (<see cref="TargetValue"/>) est autre chose qu'un seuil : c'est la valeur visee,
/// affichee a cote du realise et du budget. Un seuil declenche une alerte, un objectif non
/// atteint n'en declenche pas - sauf si l'etablissement a aussi pose une borne critique.
/// </summary>
public sealed class KpiThreshold : AuditableEntity
{
    private KpiThreshold()
    {
    }

    public KpiThreshold(
        string kpiCode,
        string? hotelUnitCode,
        decimal? favorableThreshold,
        decimal? criticalThreshold,
        decimal? targetValue,
        string? ownerRole,
        string? notes = null)
    {
        var definition = KpiCatalog.Find(kpiCode)
            ?? throw new ArgumentException($"Indicateur inconnu : {kpiCode}.", nameof(kpiCode));

        KpiCode = definition.Code;
        HotelUnitCode = string.IsNullOrWhiteSpace(hotelUnitCode)
            ? null
            : HotelUnit.NormalizeCode(hotelUnitCode);
        ScopeKey = KpiScopeKey.For(HotelUnitCode);

        Apply(favorableThreshold, criticalThreshold, targetValue, ownerRole, notes);
        IsActive = true;
    }

    /// <summary>Code du <see cref="KpiCatalog"/> auquel ces bornes s'appliquent.</summary>
    public string KpiCode { get; private set; } = string.Empty;

    /// <summary>Unite concernee, ou null pour la regle valable dans tout le groupe.</summary>
    public string? HotelUnitCode { get; private set; }

    /// <summary>
    /// Le perimetre sous forme NON NULLE, qui porte l'unicite (indicateur, perimetre) en base.
    /// Sans elle, deux regles GROUPE concurrentes pourraient coexister sur le meme indicateur et
    /// le verdict dependrait de l'ordre de lecture. Voir <see cref="KpiScopeKey"/>.
    /// </summary>
    public string ScopeKey { get; private set; } = KpiScopeKey.Group;

    /// <summary>Valeur a partir de laquelle l'indicateur est considere comme bon.</summary>
    public decimal? FavorableThreshold { get; private set; }

    /// <summary>Valeur a partir de laquelle l'indicateur appelle une decision.</summary>
    public decimal? CriticalThreshold { get; private set; }

    /// <summary>Valeur visee, affichee en regard du realise. Ne declenche aucune alerte.</summary>
    public decimal? TargetValue { get; private set; }

    /// <summary>
    /// Role responsable de l'indicateur, tel qu'il apparait sur l'alerte. Texte libre et non un
    /// utilisateur : c'est une fonction qui repond d'un indicateur, pas une personne, et les
    /// personnes changent plus vite que les seuils.
    /// </summary>
    public string? OwnerRole { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Modifie les bornes. La coherence est verifiee ICI, dans le domaine, et pas dans l'ecran :
    /// une borne favorable plus basse qu'une borne critique sur un indicateur ou la hausse est
    /// bonne rendrait tout verdict absurde, et l'API comme le poste client doivent etre
    /// proteges de la meme facon.
    /// </summary>
    public void Apply(
        decimal? favorableThreshold,
        decimal? criticalThreshold,
        decimal? targetValue,
        string? ownerRole,
        string? notes)
    {
        var definition = KpiCatalog.Require(KpiCode);

        if (favorableThreshold is null && criticalThreshold is null && targetValue is null)
        {
            throw new ArgumentException(
                "Un seuil doit porter au moins une borne ou un objectif.",
                nameof(favorableThreshold));
        }

        if (favorableThreshold is not null && criticalThreshold is not null)
        {
            var coherent = definition.Polarity switch
            {
                // Hausse favorable : le bon seuil est au-dessus du mauvais.
                KpiPolarity.HigherIsBetter => favorableThreshold.Value > criticalThreshold.Value,

                // Baisse favorable : le bon seuil est en dessous du mauvais.
                KpiPolarity.LowerIsBetter => favorableThreshold.Value < criticalThreshold.Value,

                // Indicateur neutre : aucun sens de lecture, donc aucun verdict possible.
                _ => false
            };

            if (!coherent)
            {
                throw new ArgumentException(
                    definition.Polarity == KpiPolarity.Neutral
                        ? $"L'indicateur {definition.Code} n'a pas de sens de lecture : il ne peut pas porter de seuils."
                        : $"Les bornes de {definition.Code} sont incoherentes avec son sens de lecture.",
                    nameof(criticalThreshold));
            }
        }
        else if (definition.Polarity == KpiPolarity.Neutral
            && (favorableThreshold is not null || criticalThreshold is not null))
        {
            throw new ArgumentException(
                $"L'indicateur {definition.Code} n'a pas de sens de lecture : il ne peut pas porter de seuils.",
                nameof(favorableThreshold));
        }

        FavorableThreshold = Normalize(favorableThreshold, nameof(favorableThreshold));
        CriticalThreshold = Normalize(criticalThreshold, nameof(criticalThreshold));
        TargetValue = Normalize(targetValue, nameof(targetValue));
        OwnerRole = NormalizeText(ownerRole, nameof(ownerRole), 80);
        Notes = NormalizeText(notes, nameof(notes), 500);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Une valeur de seuil est stockee sur 4 decimales (certains ratios se pilotent au
    /// millieme) ; au-dela, la valeur serait tronquee en base et le seuil applique ne serait
    /// plus celui qui a ete valide a l'ecran - meme regle que les montants budgetaires.
    /// </summary>
    private static decimal? Normalize(decimal? value, string argumentName)
    {
        if (value is null)
        {
            return null;
        }

        if (decimal.Round(value.Value, 4) != value.Value)
        {
            throw new ArgumentException(
                "Une borne ne peut pas porter plus de 4 decimales.",
                argumentName);
        }

        return value;
    }

    private static string? NormalizeText(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"La valeur ne peut pas depasser {maxLength} caracteres.", argumentName);
        }

        return trimmed;
    }
}
