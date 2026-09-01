using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Api;

/// <summary>
/// Module 10 - le socle PMS : inventaire (blocages OOO/OOS, politique d'unite, restrictions,
/// surreservation), gestes de sejour (affectation, walk-in, changement de chambre, prolongation,
/// surclassement) et exploitation (date metier, planning, arrivees, departs, presents, forecast,
/// no-shows, night audit).
///
/// Fichier de classe partielle : SendAsync, ReadResponseAsync et EnsureAuthenticated sont definis
/// dans RaqmiApiClient.cs, les aides de query dans RaqmiApiClient.Lodging.cs.
/// </summary>
public sealed partial class RaqmiApiClient
{
    // ==================================== Disponibilite ====================================

    /// <summary>
    /// Recherche de disponibilite complete. Rend la disponibilite COMMERCIALE par type - ce qui est
    /// vendable sur toute la periode - et les chambres physiques libres pour l'affectation.
    /// </summary>
    public async Task<AvailabilityResponse> SearchAvailabilityAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int adults,
        int children = 0,
        int infants = 0,
        int rooms = 1,
        string? roomTypeCode = null,
        string? customerCode = null,
        bool allowOverbooking = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>
        {
            "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            "from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "adults=" + adults.ToString(CultureInfo.InvariantCulture),
            "children=" + children.ToString(CultureInfo.InvariantCulture),
            "infants=" + infants.ToString(CultureInfo.InvariantCulture),
            "rooms=" + rooms.ToString(CultureInfo.InvariantCulture)
        };

        AppendLodgingText(query, "roomTypeCode", roomTypeCode);
        AppendLodgingText(query, "customerCode", customerCode);

        if (allowOverbooking)
        {
            query.Add("allowOverbooking=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/availability?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<AvailabilityResponse>(response, cancellationToken);
    }

    // ================================= Blocages OOO / OOS =================================

    public async Task<IReadOnlyCollection<RoomBlockResponse>> GetRoomBlocksAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        RoomBlockKind? kind,
        bool includeClosed,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };

        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);

        if (kind.HasValue)
        {
            query.Add("kind=" + Uri.EscapeDataString(kind.Value.ToString()));
        }

        if (includeClosed)
        {
            query.Add("includeClosed=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/room-blocks?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RoomBlockResponse>>(response, cancellationToken);
    }

    public async Task<RoomBlockResponse> CreateRoomBlockAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        CreateRoomBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/lodging/room-blocks?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RoomBlockResponse>(response, cancellationToken);
    }

    public async Task<RoomBlockResponse> CloseRoomBlockAsync(
        string apiBaseUrl,
        Guid id,
        DateOnly returnDate,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/room-blocks/{id}/close",
            new CloseRoomBlockRequest(returnDate),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RoomBlockResponse>(response, cancellationToken);
    }

    public async Task<RoomBlockResponse> CancelRoomBlockAsync(
        string apiBaseUrl,
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/room-blocks/{id}/cancel",
            new CancelRoomBlockRequest(reason),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RoomBlockResponse>(response, cancellationToken);
    }

    // ================================== Politique d'unite ==================================

    public async Task<LodgingPolicyResponse> GetLodgingPolicyAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/policy?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LodgingPolicyResponse>(response, cancellationToken);
    }

    public async Task<LodgingPolicyResponse> SaveLodgingPolicyAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        SaveLodgingPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            "/api/v1/lodging/policy?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<LodgingPolicyResponse>(response, cancellationToken);
    }

    // ==================================== Restrictions ====================================

    public async Task<IReadOnlyCollection<RateRestrictionResponse>> GetRestrictionsAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };

        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/restrictions?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RateRestrictionResponse>>(response, cancellationToken);
    }

    public async Task<RateRestrictionResponse> CreateRestrictionAsync(
        string apiBaseUrl,
        SaveRateRestrictionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/lodging/restrictions",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RateRestrictionResponse>(response, cancellationToken);
    }

    public async Task<RateRestrictionResponse> SetRestrictionActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/restrictions/{id}/{verb}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<RateRestrictionResponse>(response, cancellationToken);
    }

    // =================================== Surreservation ===================================

    public async Task<IReadOnlyCollection<OverbookingAllowanceResponse>> GetOverbookingAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };

        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/overbooking?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<OverbookingAllowanceResponse>>(response, cancellationToken);
    }

    public async Task<OverbookingAllowanceResponse> CreateOverbookingAsync(
        string apiBaseUrl,
        SaveOverbookingAllowanceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/lodging/overbooking",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<OverbookingAllowanceResponse>(response, cancellationToken);
    }

    public async Task<OverbookingAllowanceResponse> SetOverbookingActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = isActive ? "activate" : "deactivate";

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/overbooking/{id}/{verb}",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<OverbookingAllowanceResponse>(response, cancellationToken);
    }

    // ==================================== Gestes de sejour ====================================

    public async Task<ReservationDetailResponse> GetReservationDetailAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"/api/v1/lodging/reservations/{id}/detail",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationDetailResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> CreateWalkInAsync(
        string apiBaseUrl,
        WalkInRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/lodging/reservations/walk-in",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> AssignRoomAsync(
        string apiBaseUrl,
        Guid reservationId,
        Guid? roomId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/reservations/{reservationId}/assign-room",
            new AssignRoomRequest(roomId, reason),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> MoveRoomAsync(
        string apiBaseUrl,
        Guid reservationId,
        Guid targetRoomId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/stays/{reservationId}/room-move",
            new RoomMoveRequest(targetRoomId, reason),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> ExtendStayAsync(
        string apiBaseUrl,
        Guid reservationId,
        ExtendStayRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/stays/{reservationId}/extend",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<ReservationResponse> ChangeRoomTypeAsync(
        string apiBaseUrl,
        Guid reservationId,
        ChangeRoomTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/stays/{reservationId}/change-room-type",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReservationResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FolioResponse>> PrepareCheckOutAsync(
        string apiBaseUrl,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"/api/v1/lodging/reservations/{reservationId}/prepare-check-out",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<FolioResponse>>(response, cancellationToken);
    }

    // ===================================== Exploitation =====================================

    public async Task<BusinessDateResponse> GetBusinessDateAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/business-date?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<BusinessDateResponse>(response, cancellationToken);
    }

    public async Task<TapeChartResponse> GetTapeChartAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>
        {
            "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            "from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/tape-chart?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<TapeChartResponse>(response, cancellationToken);
    }

    public async Task<ForecastResponse> GetForecastAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly from,
        int days,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>
        {
            "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            "from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "days=" + days.ToString(CultureInfo.InvariantCulture)
        };

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/forecast?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ForecastResponse>(response, cancellationToken);
    }

    public async Task<ArrivalBoardResponse> GetArrivalsAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };
        AppendLodgingDate(query, "date", date);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/arrivals?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ArrivalBoardResponse>(response, cancellationToken);
    }

    public async Task<DepartureBoardResponse> GetDeparturesAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };
        AppendLodgingDate(query, "date", date);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/departures?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<DepartureBoardResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<InHouseGuestResponse>> GetInHouseAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/in-house?hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<InHouseGuestResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Le rapport des non-presentations. <paramref name="apply"/> faux ne fait que LIRE : il rend
    /// les candidats et la penalite que chacun declencherait, sans rien basculer.
    /// </summary>
    public async Task<NoShowSweepResponse> SweepNoShowsAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? businessDate,
        bool apply,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };
        AppendLodgingDate(query, "businessDate", businessDate);

        var path = (apply ? "/api/v1/lodging/no-shows/apply?" : "/api/v1/lodging/no-shows?")
            + string.Join('&', query);

        var response = await SendAsync(
            apiBaseUrl,
            apply ? HttpMethod.Post : HttpMethod.Get,
            path,
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<NoShowSweepResponse>(response, cancellationToken);
    }

    public async Task<NightAuditResponse> RunNightAuditAsync(
        string apiBaseUrl,
        RunNightAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/lodging/night-audit/run",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<NightAuditResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<NightAuditResponse>> GetNightAuditRunsAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string> { "hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode) };
        AppendLodgingDate(query, "from", from);
        AppendLodgingDate(query, "to", to);

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            "/api/v1/lodging/night-audit?" + string.Join('&', query),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<NightAuditResponse>>(response, cancellationToken);
    }
}
