using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Mice;

namespace RaqmiSystem.Desktop.Api;

// Module 10.6 - Groupes & MICE, volet evenementiel : appels du groupe /api/v1/mice.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec les autres
// modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string MicePath = "/api/v1/mice";

    // ------------------------------- Espaces de reception -------------------------------

    public async Task<IReadOnlyCollection<FunctionSpaceResponse>> GetFunctionSpacesAsync(
        string apiBaseUrl,
        string? hotelUnitCode = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = BuildMiceQuery(
            ("hotelUnitCode", hotelUnitCode),
            ("includeInactive", includeInactive ? "true" : null));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{MicePath}/spaces{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<FunctionSpaceResponse>>(response, cancellationToken);
    }

    public async Task<FunctionSpaceResponse> CreateFunctionSpaceAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = $"{MicePath}/spaces/{Uri.EscapeDataString(hotelUnitCode)}/{Uri.EscapeDataString(code)}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, path, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<FunctionSpaceResponse>(response, cancellationToken);
    }

    public async Task<FunctionSpaceResponse> UpdateFunctionSpaceAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = $"{MicePath}/spaces/{Uri.EscapeDataString(hotelUnitCode)}/{Uri.EscapeDataString(code)}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, path, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<FunctionSpaceResponse>(response, cancellationToken);
    }

    public async Task<FunctionSpaceResponse> SetFunctionSpaceActiveAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var verb = isActive ? "activate" : "deactivate";
        var path = $"{MicePath}/spaces/{Uri.EscapeDataString(hotelUnitCode)}/{Uri.EscapeDataString(code)}/{verb}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<FunctionSpaceResponse>(response, cancellationToken);
    }

    // ------------------------------------ Evenements ------------------------------------

    public async Task<IReadOnlyCollection<EventBookingResponse>> GetEventsAsync(
        string apiBaseUrl,
        string? hotelUnitCode = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string? functionSpaceCode = null,
        bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = BuildMiceQuery(
            ("hotelUnitCode", hotelUnitCode),
            ("from", from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("to", to?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("functionSpaceCode", functionSpaceCode),
            ("includeCancelled", includeCancelled ? "true" : null));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{MicePath}/events{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<EventBookingResponse>>(response, cancellationToken);
    }

    public async Task<EventBookingResponse> CreateEventAsync(
        string apiBaseUrl,
        CreateEventBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{MicePath}/events", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    public async Task<EventBookingResponse> RescheduleEventAsync(
        string apiBaseUrl,
        Guid id,
        RescheduleEventBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{MicePath}/events/{id}/reschedule", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    public async Task<EventBookingResponse> ConfirmEventAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{MicePath}/events/{id}/confirm", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    public async Task<EventBookingResponse> CancelEventAsync(
        string apiBaseUrl,
        Guid id,
        CancelEventBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{MicePath}/events/{id}/cancel", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    // ------------------------------- Devis et BEO -------------------------------

    public async Task<EventBookingResponse> ReplaceEventLinesAsync(
        string apiBaseUrl,
        Guid id,
        IReadOnlyCollection<EventBookingLineRequest> lines,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{MicePath}/events/{id}/lines", lines, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    public async Task<EventBookingResponse> ReplaceEventScheduleAsync(
        string apiBaseUrl,
        Guid id,
        IReadOnlyCollection<EventScheduleItemRequest> schedule,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{MicePath}/events/{id}/schedule", schedule, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    // -------------------------- Facturation evenementielle --------------------------

    /// <summary>
    /// Genere la facture brouillon de l'evenement. Exige cote serveur mice.write ET invoices.write :
    /// cette route ecrit une facture reelle.
    /// </summary>
    public async Task<EventBookingResponse> InvoiceEventAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{MicePath}/events/{id}/invoice", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EventBookingResponse>(response, cancellationToken);
    }

    // Constructeur de requete local : le BuildQuery de la classe principale a une signature figee
    // (from / to / hotelUnitCode) qui ne couvre pas les filtres de ce module.
    private static string BuildMiceQuery(params (string Key, string? Value)[] parts)
    {
        var query = parts
            .Where(part => !string.IsNullOrWhiteSpace(part.Value))
            .Select(part => $"{part.Key}={Uri.EscapeDataString(part.Value!.Trim())}")
            .ToList();

        return query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
    }
}
