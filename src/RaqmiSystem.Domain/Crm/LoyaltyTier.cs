using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// One step of the loyalty programme (Classique, Argent, Or, ...): the number of points that
/// opens it, and what it entitles the guest to.
///
/// A guest's tier is DERIVED, never stored - it is the highest active tier whose
/// <see cref="PointsThreshold"/> the point balance reaches. Storing the tier on the guest would
/// make it possible for a profile to claim a tier its own movements do not justify, and would
/// have to be recomputed for every guest each time a threshold is edited. Deriving it means the
/// programme can be re-scaled by editing the tiers alone.
/// </summary>
public sealed class LoyaltyTier : AuditableEntity
{
    private LoyaltyTier()
    {
    }

    public LoyaltyTier(string code, string label, int pointsThreshold, string? benefits = null)
    {
        Code = NormalizeCode(code);
        ApplyDetails(label, pointsThreshold, benefits);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>Point balance from which the tier is reached. Zero is the entry tier.</summary>
    public int PointsThreshold { get; private set; }

    public string? Benefits { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, int pointsThreshold, string? benefits)
    {
        ApplyDetails(label, pointsThreshold, benefits);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return CrmText.RequireCode(value, nameof(value));
    }

    private void ApplyDetails(string label, int pointsThreshold, string? benefits)
    {
        if (pointsThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pointsThreshold),
                pointsThreshold,
                "A tier threshold cannot be negative.");
        }

        Label = CrmText.Require(label, nameof(label), 160);
        PointsThreshold = pointsThreshold;
        Benefits = CrmText.Optional(benefits, nameof(benefits), 600);
    }
}
