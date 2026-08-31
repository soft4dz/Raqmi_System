using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Desktop.Api;

// Module Workflows & validations : appels des groupes /api/v1/approvals/circuits et
// /api/v1/approvals/instances. Fichier de classe partielle, pour que ce chantier n'entre
// pas en conflit avec les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string ApprovalCircuitsPath = "/api/v1/approvals/circuits";

    private const string ApprovalInstancesPath = "/api/v1/approvals/instances";

    /// <summary>
    /// Liste les circuits de validation. Les circuits inactifs ne remontent que si demande.
    /// </summary>
    public async Task<IReadOnlyCollection<ApprovalCircuitResponse>> GetApprovalCircuitsAsync(
        string apiBaseUrl,
        ApprovalSubjectType? subjectType,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildApprovalCircuitsQuery(subjectType, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ApprovalCircuitResponse>>(response, cancellationToken);
    }

    public async Task<ApprovalCircuitResponse> CreateApprovalCircuitAsync(
        string apiBaseUrl,
        CreateApprovalCircuitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, ApprovalCircuitsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalCircuitResponse>(response, cancellationToken);
    }

    public async Task<ApprovalCircuitResponse> UpdateApprovalCircuitAsync(
        string apiBaseUrl,
        string code,
        UpdateApprovalCircuitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{ApprovalCircuitsPath}/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalCircuitResponse>(response, cancellationToken);
    }

    public async Task<ApprovalCircuitResponse> SetApprovalCircuitActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{ApprovalCircuitsPath}/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalCircuitResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Historique des instances de validation : filtre par sujet, reference, statut et
    /// periode d'ouverture.
    /// </summary>
    public async Task<IReadOnlyCollection<ApprovalInstanceResponse>> GetApprovalInstancesAsync(
        string apiBaseUrl,
        ApprovalSubjectType? subjectType,
        string? subjectReference,
        ApprovalInstanceStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildApprovalInstancesQuery(subjectType, subjectReference, status, from, to),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ApprovalInstanceResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// "En attente de ma decision" : les instances dont l'etape courante requiert un des roles
    /// du profil connecte (le serveur filtre sur les claims de role du jeton).
    /// </summary>
    public async Task<IReadOnlyCollection<ApprovalInstanceResponse>> GetPendingApprovalInstancesAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{ApprovalInstancesPath}/pending", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ApprovalInstanceResponse>>(response, cancellationToken);
    }

    public async Task<ApprovalInstanceResponse> OpenApprovalInstanceAsync(
        string apiBaseUrl,
        OpenApprovalInstanceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, ApprovalInstancesPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalInstanceResponse>(response, cancellationToken);
    }

    public async Task<ApprovalInstanceResponse> ApproveApprovalInstanceAsync(
        string apiBaseUrl,
        Guid id,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{ApprovalInstancesPath}/{id}/approve", new DecideApprovalRequest(comment), includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalInstanceResponse>(response, cancellationToken);
    }

    public async Task<ApprovalInstanceResponse> RejectApprovalInstanceAsync(
        string apiBaseUrl,
        Guid id,
        string comment,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{ApprovalInstancesPath}/{id}/reject", new DecideApprovalRequest(comment), includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApprovalInstanceResponse>(response, cancellationToken);
    }

    private static string BuildApprovalCircuitsQuery(ApprovalSubjectType? subjectType, bool includeInactive)
    {
        var query = new List<string>();

        if (subjectType.HasValue)
        {
            query.Add("subjectType=" + Uri.EscapeDataString(subjectType.Value.ToString()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        return query.Count == 0
            ? ApprovalCircuitsPath
            : ApprovalCircuitsPath + "?" + string.Join("&", query);
    }

    private static string BuildApprovalInstancesQuery(
        ApprovalSubjectType? subjectType,
        string? subjectReference,
        ApprovalInstanceStatus? status,
        DateOnly? from,
        DateOnly? to)
    {
        var query = new List<string>();

        if (subjectType.HasValue)
        {
            query.Add("subjectType=" + Uri.EscapeDataString(subjectType.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(subjectReference))
        {
            query.Add("subjectReference=" + Uri.EscapeDataString(subjectReference.Trim()));
        }

        if (status.HasValue)
        {
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        if (from.HasValue)
        {
            query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        if (to.HasValue)
        {
            query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return query.Count == 0
            ? ApprovalInstancesPath
            : ApprovalInstancesPath + "?" + string.Join("&", query);
    }
}
