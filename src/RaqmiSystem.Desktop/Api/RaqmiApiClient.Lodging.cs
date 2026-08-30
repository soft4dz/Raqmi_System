using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Api;

// Module Hebergement et occupation : appels des groupes /api/v1/lodging/... et
// du miroir de resolution tarifaire /api/v1/tariffs/resolve (apercu du tarif
// d'une nuit avant creation d'une reservation).
// Fichier de classe partielle : SendAsync, ReadResponseAsync et
// EnsureAuthenticated sont definis dans RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    // ============================== Types de chambre ==============================

    public async Task<IReadOnlyCollection<RoomTypeResponse>> GetRoomTypesAsync(
        string apiBaseUrl,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildLodgingListQuery("/api/v1/lodging/room-types", hotelUnitCode, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RoomTypeResponse>>(response, cancellationToken);
    }

    // ================================== Chambres ==================================

    public async Task<IReadOnlyCollection<RoomResponse>> GetRoomsAsync(
        string apiBaseUrl,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildLodgingListQuery("/api/v1/lodging/rooms", hotelUnitCode, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RoomResponse>>(response, cancellationToken);
    }

    // ================================ Reservations ================================

    /// <summary>
    /// Liste les reservations. La periode retient toute reservation dont le sejour
    /// touche [from, to] (recouvrement), pas seulement celles qui y commencent.
    /// </summary>
    public async Task<IReadOnlyCollection<ReservationResponse>> GetReservationsAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReservationStatus? status,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);
        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);

        if (status.HasValue)
        {
            // Les enums sont serialises en chaine par l'API (JsonStringEnumConverter) :
            // la query utilise le nom du membre, comme l'attend l'endpoint.
            query.Add("status=" + Uri.EscapeDataString(status.Value.ToString()));
        }

        AppendLodgingText(query, "customerCode", customerCode);

        var path = query.Count == 0
            ? "/api/v1/lodging/reservations"
            : "/api/v1/lodging/reservations?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ReservationResponse>>(response, cancellationToken);
    }

    public async Task<ReservationResponse> CreateReservationAsync(
        string apiBaseUrl,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/lodging/reservations", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> CheckInReservationAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/reservations/{id}/check-in", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> CheckOutReservationAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/reservations/{id}/check-out", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> CancelReservationAsync(
        string apiBaseUrl,
        Guid id,
        CancelReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/reservations/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> MarkReservationNoShowAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/reservations/{id}/no-show", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    // ==================================== Folio ===================================

    public async Task<FolioResponse> GetReservationFolioAsync(
        string apiBaseUrl,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/lodging/reservations/{reservationId}/folio", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<FolioResponse>(response, cancellationToken);
    }

    public async Task<FolioResponse> AddFolioChargeAsync(
        string apiBaseUrl,
        Guid reservationId,
        AddFolioChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/reservations/{reservationId}/folio/charges", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<FolioResponse>(response, cancellationToken);
    }

    // ================================= Occupation =================================

    public async Task<OccupancyResponse> GetOccupancyAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);
        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/occupancy?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<OccupancyResponse>(response, cancellationToken);
    }

    // ============================ Resolution tarifaire ============================

    /// <summary>
    /// Miroir diagnostique de la resolution tarifaire : ce que couterait une nuit
    /// pour une unite + un type de chambre + une date, convention client comprise.
    /// Ne cree rien ; exige la permission tariffs.read (et non lodging.read). La
    /// vue s'en sert comme apercu avant creation : le montant qui fait foi reste
    /// celui fige par le serveur a la creation de la reservation.
    /// </summary>
    public async Task<ResolvedNightlyRate> ResolveNightlyRateAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly night,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);
        AppendLodgingText(query, "roomTypeCode", roomTypeCode);
        AppendLodgingDate(query, "night", night);
        AppendLodgingText(query, "customerCode", customerCode);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/tariffs/resolve?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ResolvedNightlyRate>(response, cancellationToken);
    }

    // ================================== Requetes ==================================

    private static string BuildLodgingListQuery(string basePath, string? hotelUnitCode, bool includeInactive)
    {
        var query = new List<string>();

        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        return query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);
    }

    private static void AppendLodgingDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendLodgingText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
