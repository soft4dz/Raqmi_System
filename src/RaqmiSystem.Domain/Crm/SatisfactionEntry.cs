using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// One satisfaction answer given by one guest about one unit: the Net Promoter Score question
/// ("would you recommend us", 0 to 10) plus what the guest wrote next to it.
///
/// The answer is stored RAW. Its <see cref="Category"/> and the NPS of a population are derived
/// from it at read time by <see cref="ComputeNps"/>, so a change of method never has to be
/// back-filled onto the answers already collected - and two screens can never disagree about what
/// the same set of answers is worth.
///
/// <see cref="ReservationId"/> is optional: an answer collected online months later belongs to the
/// guest without belonging to an identified stay, and refusing it would only mean losing it.
/// </summary>
public sealed class SatisfactionEntry : AuditableEntity
{
    /// <summary>Highest score of the NPS question. The scale is 0 to 10 inclusive.</summary>
    public const int MaximumScore = 10;

    private SatisfactionEntry()
    {
    }

    public SatisfactionEntry(
        string customerCode,
        string hotelUnitCode,
        DateOnly surveyDate,
        int score,
        SatisfactionSource source,
        Guid? reservationId = null,
        string? comment = null)
    {
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown satisfaction source.");
        }

        if (score is < 0 or > MaximumScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                $"A satisfaction score is between 0 and {MaximumScore}.");
        }

        CustomerCode = Customer.NormalizeCode(customerCode);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        SurveyDate = surveyDate;
        Score = score;
        Source = source;
        ReservationId = reservationId == Guid.Empty ? null : reservationId;
        Comment = CrmText.Optional(comment, nameof(comment), 2000);
    }

    public string CustomerCode { get; private set; } = string.Empty;

    public string HotelUnitCode { get; private set; } = string.Empty;

    public DateOnly SurveyDate { get; private set; }

    /// <summary>The NPS answer, 0 to 10 inclusive.</summary>
    public int Score { get; private set; }

    public SatisfactionSource Source { get; private set; }

    /// <summary>The stay the answer is about, when it is known.</summary>
    public Guid? ReservationId { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>Which side of the NPS the answer falls on. Derived, never stored.</summary>
    public NpsCategory Category => Classify(Score);

    /// <summary>
    /// The NPS cut-offs of the method, in one place: 0-6 detract, 7-8 are passive, 9-10 promote.
    /// </summary>
    public static NpsCategory Classify(int score)
    {
        return score switch
        {
            >= 9 => NpsCategory.Promoter,
            >= 7 => NpsCategory.Passive,
            _ => NpsCategory.Detractor
        };
    }

    /// <summary>
    /// The Net Promoter Score of a population: percentage of promoters minus percentage of
    /// detractors, on the -100..+100 scale, rounded to one decimal. Passives count in the total
    /// and in nothing else - that is the whole point of the measure.
    ///
    /// Returns NULL when nobody answered, rather than zero: no answer at all and a population
    /// exactly split between promoters and detractors are two very different situations, and a
    /// screen must be able to show a dash for the first.
    /// </summary>
    public static decimal? ComputeNps(int promoters, int passives, int detractors)
    {
        if (promoters < 0 || passives < 0 || detractors < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(promoters), "An answer count cannot be negative.");
        }

        var total = promoters + passives + detractors;

        if (total == 0)
        {
            return null;
        }

        return Math.Round(100m * (promoters - detractors) / total, 1, MidpointRounding.AwayFromZero);
    }
}
