using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

/// <summary>
/// CRM and guest experience (module 10.4): the 360 view of a guest, the segmentation of the
/// customer file, the loyalty programme, the marketing campaigns, the satisfaction measured by
/// NPS, and the log of the contacts with the guest.
///
/// The module owns FOUR facts about a guest: how they are qualified (segment, preferences,
/// consent), what they have earned in the programme, what they said about the establishment, and
/// who talked to them. Everything else it displays - identity, stays, invoices - is read from the
/// module that owns it at query time and never stored here, so the CRM can never end up
/// disagreeing with the front desk about how many nights a guest has slept.
/// </summary>
public interface ICrmService
{
    // Segments -----------------------------------------------------------------------------

    /// <summary>Every segment, inactive ones only on demand, with the number of guests carrying each.</summary>
    Task<IReadOnlyCollection<CustomerSegmentResponse>> ListSegmentsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerSegmentResponse>> CreateSegmentAsync(
        CreateCustomerSegmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerSegmentResponse>> UpdateSegmentAsync(
        string code,
        UpdateCustomerSegmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Activates or deactivates a segment. A segment is never deleted: the guests already
    /// carrying it, and the campaigns already run on it, must keep reading the way they happened.
    /// </summary>
    Task<ApplicationResult<CustomerSegmentResponse>> SetSegmentActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // Guest profiles and the 360 view -------------------------------------------------------

    /// <summary>
    /// The qualified guests, narrowed by search (customer code or name), by segment, or to the
    /// VIP only. Customers without a CRM profile are not listed here - they appear in the customer
    /// file, and the 360 view knows how to show one.
    /// </summary>
    Task<IReadOnlyCollection<GuestProfileResponse>> ListGuestProfilesAsync(
        string? search,
        string? segmentCode,
        bool vipOnly,
        CancellationToken cancellationToken);

    Task<ApplicationResult<GuestProfileResponse>> GetGuestProfileAsync(
        string customerCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the CRM half of a customer, creating the profile on the first save. The customer
    /// must exist in the customer file: the CRM qualifies customers, it does not invent them.
    /// </summary>
    Task<ApplicationResult<GuestProfileResponse>> SaveGuestProfileAsync(
        string customerCode,
        SaveGuestProfileRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the guest's answer to the direct-marketing question, stamped with the moment it
    /// was given. Creates the profile if the answer is the first thing known about the guest.
    /// </summary>
    Task<ApplicationResult<GuestProfileResponse>> SetMarketingConsentAsync(
        string customerCode,
        SetMarketingConsentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Everything the ERP knows about one guest, gathered for one screen.</summary>
    Task<ApplicationResult<Customer360Response>> GetCustomer360Async(
        string customerCode,
        DateOnly today,
        CancellationToken cancellationToken);

    // Loyalty ------------------------------------------------------------------------------

    Task<IReadOnlyCollection<LoyaltyTierResponse>> ListLoyaltyTiersAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<LoyaltyTierResponse>> CreateLoyaltyTierAsync(
        CreateLoyaltyTierRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<LoyaltyTierResponse>> UpdateLoyaltyTierAsync(
        string code,
        UpdateLoyaltyTierRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<LoyaltyTierResponse>> SetLoyaltyTierActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>The balance, the tier it reaches, and the movements that justify it.</summary>
    Task<ApplicationResult<LoyaltyStatementResponse>> GetLoyaltyStatementAsync(
        string customerCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Posts one movement on a guest's ledger. <paramref name="kind"/> decides the sign; a
    /// redemption or an expiry that would take the balance below zero is refused, because points
    /// the guest never earned cannot be spent.
    /// </summary>
    Task<ApplicationResult<LoyaltyStatementResponse>> RecordLoyaltyMovementAsync(
        string customerCode,
        LoyaltyTransactionKind kind,
        LoyaltyMovementRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // Campaigns ----------------------------------------------------------------------------

    Task<IReadOnlyCollection<CampaignResponse>> ListCampaignsAsync(
        CampaignStatus? status,
        string? segmentCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CampaignResponse>> GetCampaignAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CampaignResponse>> CreateCampaignAsync(
        CreateCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Rewrites a campaign. Refused once it has left the draft state.</summary>
    Task<ApplicationResult<CampaignResponse>> UpdateCampaignAsync(
        string code,
        UpdateCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Draft -> Scheduled, Scheduled -> Running, Running -> Completed.</summary>
    Task<ApplicationResult<CampaignResponse>> ScheduleCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CampaignResponse>> LaunchCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CampaignResponse>> CompleteCampaignAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CampaignResponse>> CancelCampaignAsync(
        string code,
        CancelCampaignRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Who the campaign reaches: the guests of its target segment, minus those a direct channel
    /// may not address for want of a recorded consent, minus those it has no way of reaching.
    /// </summary>
    Task<ApplicationResult<CampaignAudienceResponse>> GetCampaignAudienceAsync(
        string code,
        CancellationToken cancellationToken);

    // Satisfaction -------------------------------------------------------------------------

    Task<IReadOnlyCollection<SatisfactionEntryResponse>> ListSatisfactionEntriesAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        string? customerCode,
        NpsCategory? category,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SatisfactionEntryResponse>> RecordSatisfactionAsync(
        RecordSatisfactionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>The NPS of the period, for the group or for one unit, broken down unit by unit.</summary>
    Task<ApplicationResult<NpsSummaryResponse>> GetNpsSummaryAsync(
        DateOnly from,
        DateOnly to,
        string? hotelUnitCode,
        CancellationToken cancellationToken);

    // Interactions -------------------------------------------------------------------------

    Task<IReadOnlyCollection<GuestInteractionResponse>> ListInteractionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<GuestInteractionResponse>> LogInteractionAsync(
        LogGuestInteractionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
