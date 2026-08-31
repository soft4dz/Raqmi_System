using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// The CRM half of a customer: everything the relationship needs to know that invoicing does not.
/// It EXTENDS <see cref="Customer"/> one-to-one on <see cref="CustomerCode"/> rather than
/// duplicating it - name, fiscal identifiers and contact details keep living in the customer
/// file, which is the only place they are maintained, and the 360 view joins the two at read
/// time. A guest without a profile is a perfectly normal customer; the profile appears the day
/// someone records something about the relationship.
///
/// MARKETING CONSENT is stored with the moment it last changed, not as a bare flag. Consent is
/// what makes a direct campaign lawful (loi 18-07 on personal data), so the installation has to
/// be able to say WHEN a guest opted in or out, not only that they did. Every direct-channel
/// audience is filtered on <see cref="MarketingConsent"/>; see <see cref="Campaign"/>.
///
/// The loyalty balance is deliberately ABSENT from this entity: it is the sum of the
/// <see cref="LoyaltyTransaction"/> ledger, and a stored copy would be one more thing that can
/// silently disagree with the movements that justify it.
/// </summary>
public sealed class GuestProfile : AuditableEntity
{
    private GuestProfile()
    {
    }

    public GuestProfile(
        string customerCode,
        string? segmentCode = null,
        string? preferredLanguage = null,
        DateOnly? birthDate = null,
        string? preferences = null,
        string? notes = null,
        bool isVip = false)
    {
        CustomerCode = Customer.NormalizeCode(customerCode);
        ApplyDetails(segmentCode, preferredLanguage, birthDate, preferences, notes, isVip);
    }

    /// <summary>Code of the customer this profile extends. One profile per customer.</summary>
    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>Commercial segment the guest belongs to, or null while unqualified.</summary>
    public string? SegmentCode { get; private set; }

    /// <summary>
    /// Language the guest is addressed in ("fr", "ar", "en"). Free short text rather than an
    /// enum: the list of languages an establishment serves is a commercial decision, not a
    /// domain invariant.
    /// </summary>
    public string? PreferredLanguage { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>Stay preferences the front desk acts on: floor, bedding, quiet room, allergies.</summary>
    public string? Preferences { get; private set; }

    /// <summary>Free relationship notes, for what does not fit a preference.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Guest the establishment treats as VIP. Purely declarative - it changes how the front desk
    /// prepares an arrival, never what the guest is charged.
    /// </summary>
    public bool IsVip { get; private set; }

    /// <summary>Has the guest agreed to receive direct marketing on Email/SMS?</summary>
    public bool MarketingConsent { get; private set; }

    /// <summary>
    /// When <see cref="MarketingConsent"/> last changed, whichever way. Null while the question
    /// was never asked, which is NOT the same as a recorded refusal.
    /// </summary>
    public DateTimeOffset? MarketingConsentUpdatedAt { get; private set; }

    public void UpdateDetails(
        string? segmentCode,
        string? preferredLanguage,
        DateOnly? birthDate,
        string? preferences,
        string? notes,
        bool isVip)
    {
        ApplyDetails(segmentCode, preferredLanguage, birthDate, preferences, notes, isVip);
    }

    /// <summary>
    /// Records the guest's answer to the direct-marketing question, stamped with the moment it
    /// was given. Idempotent on the value: recording the same answer twice does not rewrite the
    /// date, so the proof kept is the date consent was actually OBTAINED, not the date a screen
    /// was saved again.
    /// </summary>
    public void SetMarketingConsent(bool consent, DateTimeOffset utcNow)
    {
        if (MarketingConsent == consent && MarketingConsentUpdatedAt is not null)
        {
            return;
        }

        MarketingConsent = consent;
        MarketingConsentUpdatedAt = utcNow;
    }

    private void ApplyDetails(
        string? segmentCode,
        string? preferredLanguage,
        DateOnly? birthDate,
        string? preferences,
        string? notes,
        bool isVip)
    {
        SegmentCode = CrmText.OptionalCode(segmentCode, nameof(segmentCode));
        PreferredLanguage = CrmText.Optional(preferredLanguage, nameof(preferredLanguage), 10)?.ToLowerInvariant();
        BirthDate = birthDate;
        Preferences = CrmText.Optional(preferences, nameof(preferences), 600);
        Notes = CrmText.Optional(notes, nameof(notes), 1000);
        IsVip = isVip;
    }
}
