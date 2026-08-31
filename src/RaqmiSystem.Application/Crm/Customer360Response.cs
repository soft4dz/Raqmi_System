using RaqmiSystem.Application.Billing;

namespace RaqmiSystem.Application.Crm;

/// <summary>
/// The 360 view of one guest: the customer file, the CRM profile, the loyalty position, what the
/// stays and the invoices amount to, what the guest thinks of the establishment, the last
/// contacts, and the campaigns addressing them today.
///
/// Everything here is READ from the modules that own it - nothing is a CRM copy. That is the
/// whole point of the screen: it is a place to look at a guest from, not a second place where the
/// truth about them is kept.
///
/// <paramref name="Profile"/> is null for a customer nobody has qualified yet, which is a normal
/// state and not an error: the rest of the view is still worth showing.
/// </summary>
public sealed record Customer360Response(
    CustomerResponse Customer,
    GuestProfileResponse? Profile,
    LoyaltyStatementResponse Loyalty,
    GuestStayStatistics Stays,
    GuestBillingStatistics Billing,
    GuestSatisfactionStatistics Satisfaction,
    IReadOnlyCollection<GuestInteractionResponse> RecentInteractions,
    IReadOnlyCollection<SatisfactionEntryResponse> RecentSurveys,
    IReadOnlyCollection<CampaignResponse> LiveCampaigns);
