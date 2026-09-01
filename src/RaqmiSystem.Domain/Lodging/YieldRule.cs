using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>Ce qui declenche une regle de yield.</summary>
public enum YieldTrigger
{
    /// <summary>Le taux d'occupation prevu de la nuit atteint ou depasse le seuil.</summary>
    OccupancyAtOrAbove = 0,

    /// <summary>Le taux d'occupation prevu de la nuit est strictement sous le seuil.</summary>
    OccupancyBelow = 1,

    /// <summary>Il reste au plus N jours avant la nuit (derniere minute).</summary>
    LeadTimeAtOrBelow = 2,

    /// <summary>Il reste au moins N jours avant la nuit (reservation anticipee).</summary>
    LeadTimeAtOrAbove = 3,

    /// <summary>La nuit tombe un des jours de semaine declares.</summary>
    DayOfWeek = 4,

    /// <summary>La nuit tombe dans la periode de la regle, sans autre condition (evenement, saison).</summary>
    Always = 5
}

/// <summary>
/// Une regle de revenue management : dans quelles conditions le tarif resolu est majore ou minore,
/// et de combien.
///
/// LE PRIX MODIFIE DOIT TOUJOURS DIRE POURQUOI. C'est la contrainte structurante de ce modele :
/// une regle appliquee laisse son code dans le tarif resolu, et de la dans la reservation. Un prix
/// qui change sans regle nommee est invalidable - ni le client, ni le controle de gestion, ni le
/// commercial ne peuvent le discuter, et personne ne peut le reproduire six mois plus tard.
///
/// UNE SEULE REGLE S'APPLIQUE PAR NUIT, celle de plus petite <see cref="Priority"/> parmi les
/// applicables. Le cumul est deliberement exclu : trois regles a +10 % qui se declenchent ensemble
/// produisent +33 %, ce que personne n'a decide et que personne ne verra venir.
/// </summary>
public sealed class YieldRule : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int LabelMaxLength = 160;
    public const int NotesMaxLength = 500;

    /// <summary>Bornes de l'ajustement : au-dela, ce n'est plus du yield mais un autre tarif.</summary>
    public const decimal MaxAdjustmentPercent = 300m;

    private YieldRule()
    {
    }

    public YieldRule(
        string hotelUnitCode,
        string code,
        string label,
        DateOnly fromDate,
        DateOnly toDate,
        YieldTrigger trigger,
        decimal thresholdValue,
        decimal adjustmentPercent,
        int priority,
        string? roomTypeCode = null,
        string? ratePlanCode = null)
    {
        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Declencheur de yield inconnu.");
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = LodgingText.RequireCode(code, nameof(code), CodeMaxLength);
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        RoomTypeCode = LodgingText.OptionalCode(roomTypeCode, nameof(roomTypeCode));
        RatePlanCode = LodgingText.OptionalCode(ratePlanCode, nameof(ratePlanCode));
        Trigger = trigger;
        IsActive = true;

        ApplyTerms(fromDate, toDate, thresholdValue, adjustmentPercent, priority);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Code de la regle, unique dans l'unite. C'est lui qui est fige dans le tarif resolu.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>Type vise. Null = tous les types.</summary>
    public string? RoomTypeCode { get; private set; }

    /// <summary>Plan tarifaire vise. Null = tous les plans.</summary>
    public string? RatePlanCode { get; private set; }

    public DateOnly FromDate { get; private set; }

    /// <summary>Derniere nuit couverte, INCLUSE.</summary>
    public DateOnly ToDate { get; private set; }

    public YieldTrigger Trigger { get; private set; }

    /// <summary>Seuil du declencheur : pourcentage d'occupation ou nombre de jours, selon le cas.</summary>
    public decimal ThresholdValue { get; private set; }

    /// <summary>
    /// Jours de semaine vises quand <see cref="Trigger"/> vaut <see cref="YieldTrigger.DayOfWeek"/>,
    /// sous la forme "MON;FRI;SAT". Null pour les autres declencheurs.
    /// </summary>
    public string? DaysOfWeek { get; private set; }

    /// <summary>Ajustement en pourcentage du tarif resolu. Positif majore, negatif minore.</summary>
    public decimal AdjustmentPercent { get; private set; }

    /// <summary>
    /// Ordre d'application : la plus PETITE priorite applicable l'emporte. Les regles les plus
    /// fortes se rangent donc en tete, ce qui rend le bareme lisible de haut en bas.
    /// </summary>
    public int Priority { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>La regle s'applique-t-elle a ce type et ce plan ?</summary>
    public bool AppliesTo(string? roomTypeCode, string? ratePlanCode)
    {
        return Matches(RoomTypeCode, roomTypeCode) && Matches(RatePlanCode, ratePlanCode);
    }

    /// <summary>
    /// La regle se declenche-t-elle pour cette nuit ? <paramref name="occupancyPercent"/> est
    /// l'occupation PREVUE de la nuit et <paramref name="leadDays"/> le nombre de jours qui la
    /// separent encore de la date de reservation.
    /// </summary>
    public bool Triggers(DateOnly night, decimal occupancyPercent, int leadDays)
    {
        if (!IsActive || night < FromDate || night > ToDate)
        {
            return false;
        }

        return Trigger switch
        {
            YieldTrigger.Always => true,
            YieldTrigger.OccupancyAtOrAbove => occupancyPercent >= ThresholdValue,
            YieldTrigger.OccupancyBelow => occupancyPercent < ThresholdValue,
            YieldTrigger.LeadTimeAtOrBelow => leadDays <= ThresholdValue,
            YieldTrigger.LeadTimeAtOrAbove => leadDays >= ThresholdValue,
            YieldTrigger.DayOfWeek => MatchesDayOfWeek(night),
            _ => false
        };
    }

    /// <summary>Applique l'ajustement a un montant, arrondi au centime.</summary>
    public decimal Apply(decimal amount)
    {
        var adjusted = amount * (1m + (AdjustmentPercent / 100m));

        return Math.Round(Math.Max(0m, adjusted), 2, MidpointRounding.AwayFromZero);
    }

    public void UpdateTerms(
        DateOnly fromDate,
        DateOnly toDate,
        decimal thresholdValue,
        decimal adjustmentPercent,
        int priority)
    {
        ApplyTerms(fromDate, toDate, thresholdValue, adjustmentPercent, priority);
    }

    public void SetScope(string? roomTypeCode, string? ratePlanCode)
    {
        RoomTypeCode = LodgingText.OptionalCode(roomTypeCode, nameof(roomTypeCode));
        RatePlanCode = LodgingText.OptionalCode(ratePlanCode, nameof(ratePlanCode));
    }

    /// <summary>Declare les jours de semaine vises. Une liste vide efface la contrainte.</summary>
    public void SetDaysOfWeek(IEnumerable<DayOfWeek>? days)
    {
        if (days is null)
        {
            DaysOfWeek = null;
            return;
        }

        var codes = days
            .Distinct()
            .OrderBy(day => (int)day)
            .Select(ToCode)
            .ToArray();

        DaysOfWeek = codes.Length == 0 ? null : string.Join(';', codes);
    }

    public void SetNotes(string? notes)
    {
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private void ApplyTerms(
        DateOnly fromDate,
        DateOnly toDate,
        decimal thresholdValue,
        decimal adjustmentPercent,
        int priority)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException(
                "La date de debut ne peut pas etre posterieure a la date de fin.",
                nameof(fromDate));
        }

        if (Math.Abs(adjustmentPercent) > MaxAdjustmentPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adjustmentPercent),
                adjustmentPercent,
                $"L'ajustement ne peut pas depasser {MaxAdjustmentPercent} % en valeur absolue.");
        }

        if (decimal.Round(adjustmentPercent, 2) != adjustmentPercent)
        {
            throw new ArgumentException(
                "L'ajustement ne peut pas porter plus de deux decimales.",
                nameof(adjustmentPercent));
        }

        if (adjustmentPercent == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adjustmentPercent),
                adjustmentPercent,
                "Une regle a 0 % ne change rien : desactivez-la plutot que de la declarer.");
        }

        if (thresholdValue < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thresholdValue),
                thresholdValue,
                "Le seuil ne peut pas etre negatif.");
        }

        FromDate = fromDate;
        ToDate = toDate;
        ThresholdValue = decimal.Round(thresholdValue, 2);
        AdjustmentPercent = adjustmentPercent;
        Priority = LodgingText.Count(priority, nameof(priority), 999);
    }

    private bool MatchesDayOfWeek(DateOnly night)
    {
        if (string.IsNullOrWhiteSpace(DaysOfWeek))
        {
            return false;
        }

        var code = ToCode(night.DayOfWeek);

        return DaysOfWeek
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => string.Equals(entry, code, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToCode(DayOfWeek day) => day switch
    {
        System.DayOfWeek.Monday => "MON",
        System.DayOfWeek.Tuesday => "TUE",
        System.DayOfWeek.Wednesday => "WED",
        System.DayOfWeek.Thursday => "THU",
        System.DayOfWeek.Friday => "FRI",
        System.DayOfWeek.Saturday => "SAT",
        System.DayOfWeek.Sunday => "SUN",
        _ => "MON"
    };

    private static bool Matches(string? ruleValue, string? requestValue)
    {
        if (ruleValue is null)
        {
            return true;
        }

        return requestValue is not null
            && string.Equals(ruleValue, requestValue.Trim().ToUpperInvariant(), StringComparison.Ordinal);
    }
}
