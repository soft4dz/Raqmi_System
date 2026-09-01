using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// La valeur d'un indicateur, pour une periode et un perimetre, conservee telle qu'elle a ete
/// calculee.
///
/// POURQUOI HISTORISER UN CHIFFRE QU'ON SAIT RECALCULER. Parce qu'un recalcul ne rend pas
/// toujours le meme resultat : une facture emise en retard, une recette validee apres coup, un
/// bulletin de paie corrige changent le passe. Sans instantane, la courbe pluriannuelle du
/// RevPAR se reecrirait toute seule a chaque ouverture d'ecran, et le chiffre communique au
/// conseil d'administration ne serait plus retrouvable trois mois plus tard.
///
/// LA REGLE DE L'INSTANTANE CLOTURE. Un instantane <see cref="KpiSnapshotStatus.Provisional"/>
/// est reecrit sans facon par le recalcul suivant. Un instantane
/// <see cref="KpiSnapshotStatus.Closed"/> ne l'est JAMAIS : il correspond a une cloture
/// officielle. Si le recalcul aboutit a autre chose, le moteur ne corrige rien en silence, il
/// expose la divergence - meme discipline que la comptabilite, ou une ecriture comptabilisee se
/// corrige par une extourne et jamais par une modification.
///
/// LA VERSION DE FORMULE est copiee depuis le catalogue au moment du calcul. Sans elle, une
/// courbe melangerait des valeurs obtenues par deux formules differentes sans que personne ne
/// puisse s'en apercevoir : c'est la seule chose qui rend un historique KPI relisible apres une
/// evolution du moteur.
///
/// NUMERATEUR ET DENOMINATEUR sont conserves a cote de la valeur, pour deux raisons pratiques :
/// ils permettent de reconsolider un groupe a partir des instantanes par unite sans recharger
/// les transactions (somme des numerateurs / somme des denominateurs, voir
/// <see cref="KpiAggregation.RatioOfSums"/>), et ils rendent le chiffre verifiable a la main.
/// </summary>
public sealed class KpiSnapshot : AuditableEntity
{
    private KpiSnapshot()
    {
    }

    public KpiSnapshot(
        string kpiCode,
        string? hotelUnitCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        KpiPeriodGranularity granularity,
        decimal? value,
        decimal? numerator,
        decimal? denominator,
        KpiQuality quality,
        int formulaVersion,
        DateTimeOffset calculatedAt)
    {
        var definition = KpiCatalog.Find(kpiCode)
            ?? throw new ArgumentException($"Indicateur inconnu : {kpiCode}.", nameof(kpiCode));

        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                "La fin de periode ne peut pas preceder son debut.",
                nameof(periodEnd));
        }

        if (!Enum.IsDefined(granularity))
        {
            throw new ArgumentOutOfRangeException(nameof(granularity), granularity, "Grain de periode inconnu.");
        }

        if (!Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Statut de qualite inconnu.");
        }

        KpiCode = definition.Code;
        HotelUnitCode = string.IsNullOrWhiteSpace(hotelUnitCode)
            ? null
            : HotelUnit.NormalizeCode(hotelUnitCode);
        ScopeKey = KpiScopeKey.For(HotelUnitCode);
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Granularity = granularity;
        Value = value;
        Numerator = numerator;
        Denominator = denominator;
        Quality = quality;
        FormulaVersion = formulaVersion;
        CalculatedAt = calculatedAt;
        Status = KpiSnapshotStatus.Provisional;
    }

    public string KpiCode { get; private set; } = string.Empty;

    /// <summary>Unite mesuree, ou null pour la valeur consolidee du groupe.</summary>
    public string? HotelUnitCode { get; private set; }

    /// <summary>
    /// Le perimetre sous forme NON NULLE, pour que l'unicite (indicateur, perimetre, periode)
    /// soit une contrainte de base de donnees et pas une esperance : PostgreSQL comme SQLite
    /// considerent deux NULL comme distincts dans un index unique, si bien qu'un index portant
    /// directement <see cref="HotelUnitCode"/> laisserait passer autant d'instantanes GROUPE
    /// concurrents qu'on veut - et personne ne saurait lequel fait foi. Voir
    /// <see cref="KpiScopeKey"/>.
    /// </summary>
    public string ScopeKey { get; private set; } = KpiScopeKey.Group;

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public KpiPeriodGranularity Granularity { get; private set; }

    /// <summary>
    /// La valeur mesuree, ou null quand elle n'existe pas (denominateur nul, donnee manquante).
    /// Le null est porteur de sens et n'est jamais remplace par zero : voir
    /// <see cref="KpiMath"/>.
    /// </summary>
    public decimal? Value { get; private set; }

    public decimal? Numerator { get; private set; }

    public decimal? Denominator { get; private set; }

    public KpiQuality Quality { get; private set; }

    public int FormulaVersion { get; private set; }

    public DateTimeOffset CalculatedAt { get; private set; }

    public KpiSnapshotStatus Status { get; private set; } = KpiSnapshotStatus.Provisional;

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public bool IsClosed => Status == KpiSnapshotStatus.Closed;

    /// <summary>
    /// Remplace la valeur par celle d'un nouveau calcul. Refuse sur un instantane cloture : la
    /// garantie tient ici, dans l'entite, et pas dans le service, pour qu'aucun chemin d'ecriture
    /// - API, tache planifiee, correctif ponctuel - ne puisse la contourner.
    /// </summary>
    public void Refresh(
        decimal? value,
        decimal? numerator,
        decimal? denominator,
        KpiQuality quality,
        int formulaVersion,
        DateTimeOffset calculatedAt)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException(
                "Un instantane cloture ne peut pas etre recalcule : il correspond a une cloture officielle.");
        }

        Value = value;
        Numerator = numerator;
        Denominator = denominator;
        Quality = quality;
        FormulaVersion = formulaVersion;
        CalculatedAt = calculatedAt;
    }

    /// <summary>
    /// Fige l'instantane. Idempotent au sens strict du refus : cloturer deux fois est une erreur
    /// d'appel, pas un no-op silencieux, car la seconde cloture ecraserait la trace de qui a
    /// fige le chiffre et quand.
    /// </summary>
    public void Close(string userName, DateTimeOffset utcNow)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Cet instantane est deja cloture.");
        }

        Status = KpiSnapshotStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }

    /// <summary>
    /// La valeur recalculee diverge-t-elle de celle qui a ete figee ? Comparaison exacte sur des
    /// decimales deja arrondies au centieme par le moteur : a ce grain, une difference est une
    /// vraie difference et non un artefact de virgule flottante.
    /// </summary>
    public bool DivergesFrom(decimal? recomputedValue)
    {
        return Value != recomputedValue;
    }
}
