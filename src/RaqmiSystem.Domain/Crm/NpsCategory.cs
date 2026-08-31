namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// The three families a Net Promoter Score answer falls into. The cut-offs (0-6, 7-8, 9-10) are
/// those of the NPS method itself, not a local convention: they are what makes a score
/// comparable with anyone else's. Never stored - always derived from the answer, see
/// <see cref="SatisfactionEntry.Category"/>.
/// </summary>
public enum NpsCategory
{
    Detractor = 0,
    Passive = 1,
    Promoter = 2
}
