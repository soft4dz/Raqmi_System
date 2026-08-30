using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Desktop.Api;

// Appels du module Comptabilite SCF (/api/v1/accounting/...) : plan comptable,
// journaux, ecritures et balance generale.
//
// Fichier de classe partielle : SendAsync, ReadResponseAsync et
// EnsureAuthenticated sont definis dans RaqmiApiClient.cs.
//
// Noter ce que ce fichier n'expose pas : aucune suppression, ni de compte, ni de
// journal, ni d'ecriture. Un compte se desactive, une ecriture comptabilisee se
// corrige par une extourne (POST .../reverse) qui cree une ecriture inverse.
public sealed partial class RaqmiApiClient
{
    // ================================ Plan comptable ================================

    /// <summary>
    /// Les sept classes du SCF et, pour chacune, les natures de compte admises.
    /// Servi par le catalogue du domaine : c'est la structure de la nomenclature,
    /// pas une donnee de l'etablissement.
    /// </summary>
    public async Task<IReadOnlyCollection<AccountClassResponse>> GetAccountClassesAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/accounting/account-classes", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<AccountClassResponse>>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartAccountResponse>> GetChartAccountsAsync(
        string apiBaseUrl,
        string? search,
        int? accountClass,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendAccountingText(query, "search", search);

        if (accountClass.HasValue)
        {
            query.Add("accountClass=" + accountClass.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildAccountingPath("/api/v1/accounting/accounts", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ChartAccountResponse>>(response, cancellationToken);
    }

    public async Task<ChartAccountResponse> CreateChartAccountAsync(
        string apiBaseUrl,
        CreateChartAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/accounting/accounts", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ChartAccountResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Corrige le libelle et la nature d'un compte. Le code n'est pas modifiable :
    /// il porte la classe du compte et toutes les lignes deja comptabilisees le
    /// referencent.
    /// </summary>
    public async Task<ChartAccountResponse> UpdateChartAccountAsync(
        string apiBaseUrl,
        string code,
        UpdateChartAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/accounting/accounts/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ChartAccountResponse>(response, cancellationToken);
    }

    public async Task<ChartAccountResponse> SetChartAccountActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/accounting/accounts/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ChartAccountResponse>(response, cancellationToken);
    }

    // =================================== Journaux ===================================

    public async Task<IReadOnlyCollection<AccountingJournalResponse>> GetAccountingJournalsAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeInactive ? "?includeInactive=true" : string.Empty;
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/accounting/journals{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<AccountingJournalResponse>>(response, cancellationToken);
    }

    // =================================== Ecritures ==================================

    public async Task<IReadOnlyCollection<JournalEntryResponse>> GetJournalEntriesAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? journalCode,
        EntryStatus? status,
        string? accountCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendAccountingDate(query, "from", from);
        AppendAccountingDate(query, "to", to);
        AppendAccountingText(query, "journalCode", journalCode);
        AppendAccountingText(query, "accountCode", accountCode);

        // Les enums sont serialises en chaine par l'API (JsonStringEnumConverter) :
        // la query porte donc le nom du membre, comme l'attend l'endpoint.
        if (status.HasValue)
        {
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildAccountingPath("/api/v1/accounting/entries", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<JournalEntryResponse>>(response, cancellationToken);
    }

    public async Task<JournalEntryResponse> GetJournalEntryAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/accounting/entries/{id}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Cree une ecriture en brouillon. Le brouillon a le droit d'etre desequilibre :
    /// c'est la comptabilisation qui exige l'equilibre.
    /// </summary>
    public async Task<JournalEntryResponse> CreateJournalEntryAsync(
        string apiBaseUrl,
        CreateJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/accounting/entries", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Remplace les lignes d'un brouillon. Refuse (409) sur une ecriture
    /// comptabilisee, qui est immuable.
    /// </summary>
    public async Task<JournalEntryResponse> UpdateJournalEntryLinesAsync(
        string apiBaseUrl,
        Guid id,
        UpdateJournalEntryLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/accounting/entries/{id}/lines", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    /// <summary>Abandonne un brouillon. Sans objet sur une ecriture comptabilisee.</summary>
    public async Task<JournalEntryResponse> CancelJournalEntryAsync(
        string apiBaseUrl,
        Guid id,
        CancelJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/accounting/entries/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Comptabilise le brouillon (permission accounting.post). L'ecriture devient
    /// immuable.
    /// </summary>
    public async Task<JournalEntryResponse> PostJournalEntryAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/accounting/entries/{id}/post", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Extourne une ecriture comptabilisee (permission accounting.post) et renvoie
    /// L'ECRITURE D'EXTOURNE, pas l'ecriture corrigee : celle-ci reste comptabilisee
    /// et porte desormais ReversedByEntryId.
    /// </summary>
    public async Task<JournalEntryResponse> ReverseJournalEntryAsync(
        string apiBaseUrl,
        Guid id,
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/accounting/entries/{id}/reverse", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<JournalEntryResponse>(response, cancellationToken);
    }

    // ==================================== Balance ===================================

    /// <summary>
    /// Balance generale de la periode. Les deux bornes sont incluses et facultatives.
    /// La reponse porte elle-meme PostedEntriesOnly : seules les ecritures
    /// comptabilisees sont comptees.
    /// </summary>
    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendAccountingDate(query, "from", from);
        AppendAccountingDate(query, "to", to);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildAccountingPath("/api/v1/accounting/trial-balance", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<TrialBalanceResponse>(response, cancellationToken);
    }

    // ==================================== Requetes ==================================

    private static string BuildAccountingPath(string basePath, List<string> query)
    {
        return query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);
    }

    private static void AppendAccountingDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendAccountingText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
