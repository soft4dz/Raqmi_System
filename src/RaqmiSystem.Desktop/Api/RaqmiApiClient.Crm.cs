using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Desktop.Api;

// Module CRM et experience client : appels du groupe /api/v1/crm/...
// Fichier de classe partielle : SendAsync, ReadResponseAsync et EnsureAuthenticated
// sont definis dans RaqmiApiClient.cs. Les aides de requete sont prefixees Crm pour
// ne pas entrer en conflit avec celles des autres modules.
public sealed partial class RaqmiApiClient
{
    private const string CrmPath = "/api/v1/crm";

    // ================================== Segments ==================================

    public async Task<IReadOnlyCollection<CustomerSegmentResponse>> GetCrmSegmentsAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeInactive ? "?includeInactive=true" : string.Empty;

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{CrmPath}/segments{query}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<CustomerSegmentResponse>>(response, cancellationToken);
    }

    public async Task<CustomerSegmentResponse> CreateCrmSegmentAsync(
        string apiBaseUrl,
        CreateCustomerSegmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/segments",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CustomerSegmentResponse>(response, cancellationToken);
    }

    public async Task<CustomerSegmentResponse> UpdateCrmSegmentAsync(
        string apiBaseUrl,
        string code,
        UpdateCustomerSegmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{CrmPath}/segments/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CustomerSegmentResponse>(response, cancellationToken);
    }

    public async Task<CustomerSegmentResponse> SetCrmSegmentActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/segments/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CustomerSegmentResponse>(response, cancellationToken);
    }

    // ============================== Clients qualifies ==============================

    public async Task<IReadOnlyCollection<GuestProfileResponse>> GetCrmGuestsAsync(
        string apiBaseUrl,
        string? search,
        string? segmentCode,
        bool vipOnly,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendCrmText(query, "search", search);
        AppendCrmText(query, "segmentCode", segmentCode);

        if (vipOnly)
        {
            query.Add("vipOnly=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCrmPath($"{CrmPath}/guests", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<GuestProfileResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Vue 360 d'un client. <paramref name="today"/> est la date du POSTE : ce qui est
    /// « en cours » (campagnes actives, sejours a venir) se lit dans le jour de
    /// l'utilisateur, pas dans celui du serveur.
    /// </summary>
    public async Task<Customer360Response> GetCrmCustomer360Async(
        string apiBaseUrl,
        string customerCode,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{CrmPath}/guests/{Uri.EscapeDataString(customerCode)}/360?today={FormatCrmDate(today)}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<Customer360Response>(response, cancellationToken);
    }

    public async Task<GuestProfileResponse> SaveCrmGuestProfileAsync(
        string apiBaseUrl,
        string customerCode,
        SaveGuestProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{CrmPath}/guests/{Uri.EscapeDataString(customerCode)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<GuestProfileResponse>(response, cancellationToken);
    }

    public async Task<GuestProfileResponse> SetCrmMarketingConsentAsync(
        string apiBaseUrl,
        string customerCode,
        bool consent,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/guests/{Uri.EscapeDataString(customerCode)}/marketing-consent",
            new SetMarketingConsentRequest(consent),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<GuestProfileResponse>(response, cancellationToken);
    }

    // ================================== Fidelite ==================================

    public async Task<IReadOnlyCollection<LoyaltyTierResponse>> GetCrmLoyaltyTiersAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeInactive ? "?includeInactive=true" : string.Empty;

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{CrmPath}/loyalty/tiers{query}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<LoyaltyTierResponse>>(response, cancellationToken);
    }

    public async Task<LoyaltyTierResponse> CreateCrmLoyaltyTierAsync(
        string apiBaseUrl,
        CreateLoyaltyTierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/loyalty/tiers",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LoyaltyTierResponse>(response, cancellationToken);
    }

    public async Task<LoyaltyTierResponse> UpdateCrmLoyaltyTierAsync(
        string apiBaseUrl,
        string code,
        UpdateLoyaltyTierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{CrmPath}/loyalty/tiers/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LoyaltyTierResponse>(response, cancellationToken);
    }

    public async Task<LoyaltyTierResponse> SetCrmLoyaltyTierActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/loyalty/tiers/{Uri.EscapeDataString(code)}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LoyaltyTierResponse>(response, cancellationToken);
    }

    public async Task<LoyaltyStatementResponse> GetCrmLoyaltyStatementAsync(
        string apiBaseUrl,
        string customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{CrmPath}/loyalty/accounts/{Uri.EscapeDataString(customerCode)}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LoyaltyStatementResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Passe un mouvement sur le compte de fidelite. Le SENS vient du type de mouvement
    /// (l'operation appelee cote serveur), jamais du signe saisi : la vue envoie donc
    /// toujours une quantite de points positive, sauf pour une correction, seul
    /// mouvement qui va reellement dans les deux sens.
    /// </summary>
    public async Task<LoyaltyStatementResponse> RecordCrmLoyaltyMovementAsync(
        string apiBaseUrl,
        string customerCode,
        LoyaltyTransactionKind kind,
        LoyaltyMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = kind switch
        {
            LoyaltyTransactionKind.Earn => "earn",
            LoyaltyTransactionKind.Redeem => "redeem",
            LoyaltyTransactionKind.Expiry => "expire",
            _ => "adjust"
        };

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/loyalty/accounts/{Uri.EscapeDataString(customerCode)}/{verb}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LoyaltyStatementResponse>(response, cancellationToken);
    }

    // ================================== Campagnes ==================================

    public async Task<IReadOnlyCollection<CampaignResponse>> GetCrmCampaignsAsync(
        string apiBaseUrl,
        CampaignStatus? status,
        string? segmentCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendCrmText(query, "status", status?.ToString());
        AppendCrmText(query, "segmentCode", segmentCode);
        AppendCrmDate(query, "from", from);
        AppendCrmDate(query, "to", to);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCrmPath($"{CrmPath}/campaigns", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<CampaignResponse>>(response, cancellationToken);
    }

    public async Task<CampaignAudienceResponse> GetCrmCampaignAudienceAsync(
        string apiBaseUrl,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{CrmPath}/campaigns/{Uri.EscapeDataString(code)}/audience",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CampaignAudienceResponse>(response, cancellationToken);
    }

    public async Task<CampaignResponse> CreateCrmCampaignAsync(
        string apiBaseUrl,
        CreateCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/campaigns",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CampaignResponse>(response, cancellationToken);
    }

    public async Task<CampaignResponse> UpdateCrmCampaignAsync(
        string apiBaseUrl,
        string code,
        UpdateCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{CrmPath}/campaigns/{Uri.EscapeDataString(code)}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CampaignResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Transition de cycle de vie d'une campagne : "schedule", "launch", "complete" ou
    /// "cancel". La regle de passage est appliquee par le serveur - la vue ne fait que
    /// demander.
    /// </summary>
    public async Task<CampaignResponse> TransitionCrmCampaignAsync(
        string apiBaseUrl,
        string code,
        string transition,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/campaigns/{Uri.EscapeDataString(code)}/{transition}",
            body,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CampaignResponse>(response, cancellationToken);
    }

    // ================================= Satisfaction =================================

    public async Task<IReadOnlyCollection<SatisfactionEntryResponse>> GetCrmSatisfactionAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        string? customerCode,
        NpsCategory? category,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendCrmDate(query, "from", from);
        AppendCrmDate(query, "to", to);
        AppendCrmText(query, "hotelUnitCode", hotelUnitCode);
        AppendCrmText(query, "customerCode", customerCode);
        AppendCrmText(query, "category", category?.ToString());

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCrmPath($"{CrmPath}/satisfaction", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<SatisfactionEntryResponse>>(response, cancellationToken);
    }

    public async Task<NpsSummaryResponse> GetCrmNpsSummaryAsync(
        string apiBaseUrl,
        DateOnly from,
        DateOnly to,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendCrmDate(query, "from", from);
        AppendCrmDate(query, "to", to);
        AppendCrmText(query, "hotelUnitCode", hotelUnitCode);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCrmPath($"{CrmPath}/satisfaction/nps", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<NpsSummaryResponse>(response, cancellationToken);
    }

    public async Task<SatisfactionEntryResponse> RecordCrmSatisfactionAsync(
        string apiBaseUrl,
        RecordSatisfactionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/satisfaction",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<SatisfactionEntryResponse>(response, cancellationToken);
    }

    // ================================== Contacts ==================================

    public async Task<IReadOnlyCollection<GuestInteractionResponse>> GetCrmInteractionsAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendCrmDate(query, "from", from);
        AppendCrmDate(query, "to", to);
        AppendCrmText(query, "customerCode", customerCode);
        AppendCrmText(query, "hotelUnitCode", hotelUnitCode);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildCrmPath($"{CrmPath}/interactions", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<GuestInteractionResponse>>(response, cancellationToken);
    }

    public async Task<GuestInteractionResponse> LogCrmInteractionAsync(
        string apiBaseUrl,
        LogGuestInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{CrmPath}/interactions",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<GuestInteractionResponse>(response, cancellationToken);
    }

    // =================================== Requetes ===================================

    private static string BuildCrmPath(string path, IReadOnlyCollection<string> query)
    {
        return query.Count == 0 ? path : path + "?" + string.Join("&", query);
    }

    private static void AppendCrmText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static void AppendCrmDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add($"{name}={FormatCrmDate(value.Value)}");
        }
    }

    // Format ISO invariant : la date part telle que le serveur l'attend, quelle que
    // soit la culture du poste.
    private static string FormatCrmDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
