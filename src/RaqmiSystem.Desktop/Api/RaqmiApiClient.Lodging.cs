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

    // ============================== Disponibilites ================================

    /// <summary>
    /// Flux de reservation "dates d'abord" : toutes les chambres actives de l'unite
    /// pouvant accueillir le groupe et libres sur [from, to), chacune tarifee nuit
    /// par nuit (convention du client appliquee quand customerCode est fourni).
    /// Une chambre libre que le module tarifaire ne sait pas tarifer revient avec
    /// HasRate=false et le message du resolveur : un trou de couverture tarifaire
    /// doit se voir, pas se deguiser en occupation.
    /// </summary>
    public async Task<AvailabilityResponse> GetAvailabilityAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int guests,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);
        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);
        query.Add("guests=" + guests.ToString(CultureInfo.InvariantCulture));
        AppendLodgingText(query, "customerCode", customerCode);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/availability?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<AvailabilityResponse>(response, cancellationToken);
    }

    // ================================ Reception ===================================

    /// <summary>
    /// Instantane du comptoir d'une unite pour une journee : arrivees et departs du
    /// jour (avec soldes de folio), listes de retards (arrivees non honorees,
    /// departs depasses), presents de la nuit et occupation du jour - en un appel.
    /// </summary>
    public async Task<FrontDeskResponse> GetFrontDeskAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendLodgingText(query, "hotelUnitCode", hotelUnitCode);
        AppendLodgingDate(query, "date", date);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/front-desk?" + string.Join("&", query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<FrontDeskResponse>(response, cancellationToken);
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

    // ==================== Parametrage des chambres (types, chambres, couchage) ====================
    // Ces appels manquaient : l'API les exposait depuis le debut, le client ne les a jamais
    // appeles, et l'ecran de parametrage n'existait donc pas. C'est ce trou que ce bloc comble.

    public async Task<RoomTypeResponse> CreateRoomTypeAsync(
        string apiBaseUrl,
        CreateRoomTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/lodging/room-types", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomTypeResponse>(response, cancellationToken);
    }

    public async Task<RoomTypeResponse> UpdateRoomTypeAsync(
        string apiBaseUrl,
        Guid id,
        UpdateRoomTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/lodging/room-types/{id}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomTypeResponse>(response, cancellationToken);
    }

    public async Task<RoomTypeResponse> SetRoomTypeActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/room-types/{id}/{verb}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomTypeResponse>(response, cancellationToken);
    }

    public async Task<RoomResponse> CreateRoomAsync(
        string apiBaseUrl,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/lodging/rooms", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomResponse>(response, cancellationToken);
    }

    public async Task<RoomResponse> UpdateRoomAsync(
        string apiBaseUrl,
        Guid id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/lodging/rooms/{id}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomResponse>(response, cancellationToken);
    }

    public async Task<RoomResponse> SetRoomActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/lodging/rooms/{id}/{verb}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RoomResponse>(response, cancellationToken);
    }
}
