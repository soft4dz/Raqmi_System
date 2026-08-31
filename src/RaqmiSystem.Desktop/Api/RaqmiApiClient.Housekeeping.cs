using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Desktop.Api;

// Module Housekeeping et chambres : appels du groupe /api/v1/housekeeping/...
// Fichier de classe partielle : SendAsync, ReadResponseAsync et EnsureAuthenticated
// sont definis dans RaqmiApiClient.cs. Les aides de requete sont prefixees
// Housekeeping pour ne pas entrer en conflit avec celles des autres modules.
public sealed partial class RaqmiApiClient
{
    private const string HousekeepingPath = "/api/v1/housekeeping";

    // ============================= Tableau des chambres =============================

    /// <summary>
    /// Tableau des chambres d'une unite pour une date : etat de proprete, ce que les
    /// reservations attendent de la chambre, et la tache du jour quand il y en a une.
    /// </summary>
    public async Task<RoomBoardResponse> GetHousekeepingBoardAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendHousekeepingText(query, "hotelUnitCode", hotelUnitCode);
        AppendHousekeepingDate(query, "date", date);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{HousekeepingPath}/board?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RoomBoardResponse>(response, cancellationToken);
    }

    /// <summary>Planning des equipes : charge de chaque agent et taches non affectees.</summary>
    public async Task<HousekeepingDaySheetResponse> GetHousekeepingDaySheetAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendHousekeepingText(query, "hotelUnitCode", hotelUnitCode);
        AppendHousekeepingDate(query, "date", date);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{HousekeepingPath}/day-sheet?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<HousekeepingDaySheetResponse>(response, cancellationToken);
    }

    public async Task<RoomConditionResponse> SetRoomConditionAsync(
        string apiBaseUrl,
        Guid roomId,
        SetRoomConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/rooms/{roomId}/condition",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RoomConditionResponse>(response, cancellationToken);
    }

    // ================================== Taches ==================================

    public async Task<IReadOnlyCollection<HousekeepingTaskResponse>> GetHousekeepingTasksAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        HousekeepingTaskStatus? status,
        string? assignedTo,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendHousekeepingDate(query, "from", from);
        AppendHousekeepingDate(query, "to", to);
        AppendHousekeepingText(query, "hotelUnitCode", hotelUnitCode);

        if (status.HasValue)
        {
            // Les enums sont serialises en chaine par l'API (JsonStringEnumConverter) :
            // la query utilise le nom du membre, comme l'attend l'endpoint.
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        AppendHousekeepingText(query, "assignedTo", assignedTo);

        var path = query.Count == 0
            ? $"{HousekeepingPath}/tasks"
            : $"{HousekeepingPath}/tasks?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<HousekeepingTaskResponse>>(response, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> CreateHousekeepingTaskAsync(
        string apiBaseUrl,
        CreateHousekeepingTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/tasks",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<HousekeepingTaskResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Genere la feuille du jour depuis les reservations. Idempotent cote serveur : la
    /// reponse dit combien de taches ont ete creees et combien existaient deja.
    /// </summary>
    public async Task<GenerateHousekeepingTasksResponse> GenerateHousekeepingTasksAsync(
        string apiBaseUrl,
        GenerateHousekeepingTasksRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/tasks/generate",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<GenerateHousekeepingTasksResponse>(response, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> AssignHousekeepingTaskAsync(
        string apiBaseUrl,
        Guid id,
        AssignHousekeepingTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostHousekeepingTaskActionAsync(apiBaseUrl, id, "assign", request, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> StartHousekeepingTaskAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await PostHousekeepingTaskActionAsync(apiBaseUrl, id, "start", null, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> CompleteHousekeepingTaskAsync(
        string apiBaseUrl,
        Guid id,
        CompleteHousekeepingTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostHousekeepingTaskActionAsync(apiBaseUrl, id, "complete", request, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> InspectHousekeepingTaskAsync(
        string apiBaseUrl,
        Guid id,
        InspectHousekeepingTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostHousekeepingTaskActionAsync(apiBaseUrl, id, "inspect", request, cancellationToken);
    }

    public async Task<HousekeepingTaskResponse> CancelHousekeepingTaskAsync(
        string apiBaseUrl,
        Guid id,
        CancelHousekeepingTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostHousekeepingTaskActionAsync(apiBaseUrl, id, "cancel", request, cancellationToken);
    }

    // ================================== Minibar ==================================

    public async Task<IReadOnlyCollection<MinibarItemResponse>> GetMinibarItemsAsync(
        string apiBaseUrl,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendHousekeepingText(query, "hotelUnitCode", hotelUnitCode);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = query.Count == 0
            ? $"{HousekeepingPath}/minibar/items"
            : $"{HousekeepingPath}/minibar/items?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<MinibarItemResponse>>(response, cancellationToken);
    }

    public async Task<MinibarItemResponse> CreateMinibarItemAsync(
        string apiBaseUrl,
        CreateMinibarItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/minibar/items",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<MinibarItemResponse>(response, cancellationToken);
    }

    public async Task<MinibarItemResponse> UpdateMinibarItemAsync(
        string apiBaseUrl,
        Guid id,
        UpdateMinibarItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{HousekeepingPath}/minibar/items/{id}",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<MinibarItemResponse>(response, cancellationToken);
    }

    public async Task<MinibarItemResponse> SetMinibarItemActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/minibar/items/{id}/{action}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<MinibarItemResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MinibarConsumptionResponse>> GetMinibarConsumptionsAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        Guid? reservationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();
        AppendHousekeepingDate(query, "from", from);
        AppendHousekeepingDate(query, "to", to);
        AppendHousekeepingText(query, "hotelUnitCode", hotelUnitCode);

        if (reservationId.HasValue)
        {
            query.Add("reservationId=" + Uri.EscapeDataString(reservationId.Value.ToString()));
        }

        var path = query.Count == 0
            ? $"{HousekeepingPath}/minibar/consumptions"
            : $"{HousekeepingPath}/minibar/consumptions?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<MinibarConsumptionResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Enregistre une consommation minibar ET la porte sur le folio du sejour, en une
    /// seule transaction cote serveur.
    /// </summary>
    public async Task<MinibarConsumptionResponse> RecordMinibarConsumptionAsync(
        string apiBaseUrl,
        RecordMinibarConsumptionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/minibar/consumptions",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<MinibarConsumptionResponse>(response, cancellationToken);
    }

    // ================================== Requetes ==================================

    private async Task<HousekeepingTaskResponse> PostHousekeepingTaskActionAsync(
        string apiBaseUrl,
        Guid id,
        string action,
        object? payload,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{HousekeepingPath}/tasks/{id}/{action}",
            payload,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<HousekeepingTaskResponse>(response, cancellationToken);
    }

    private static void AppendHousekeepingDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendHousekeepingText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
