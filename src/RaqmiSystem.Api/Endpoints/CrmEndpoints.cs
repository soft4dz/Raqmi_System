using RaqmiSystem.Application.Crm;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// CRM and guest experience (module 10.4): the 360 view of a guest, the segmentation of the
/// customer file, the loyalty programme, the marketing campaigns, the satisfaction measured by
/// NPS, and the log of the contacts with the guest.
///
/// Permissions (policy names are the permission keys registered in Program.cs from
/// PermissionCatalog):
///   - "crm.read"    every GET;
///   - "crm.write"   qualifying a guest (segment, preferences, marketing consent), the segments,
///                   the loyalty tiers, the campaigns and their lifecycle, recording a
///                   satisfaction answer and logging a contact;
///   - "crm.loyalty" the four movements of the point ledger - the ONE act that moves something
///                   the guest can redeem, kept apart from the rest of the module's writing.
///
/// The four ledger movements are FOUR ROUTES rather than one taking the kind in its body: the
/// kind decides the sign of the movement, so leaving it to the payload would let a redemption ask
/// to be credited. The body carries a quantity of points, never a sign.
/// </summary>
internal static class CrmEndpoints
{
    public static RouteGroupBuilder MapCrmEndpoints(this RouteGroupBuilder api)
    {
        MapSegmentEndpoints(api);
        MapGuestEndpoints(api);
        MapLoyaltyEndpoints(api);
        MapCampaignEndpoints(api);
        MapSatisfactionEndpoints(api);
        MapInteractionEndpoints(api);
        return api;
    }

    private static void MapSegmentEndpoints(RouteGroupBuilder api)
    {
        var segments = api.MapGroup("/crm/segments")
            .WithTags("CRM - Segments");

        segments.MapGet("", async (
            bool? includeInactive,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListSegmentsAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        segments.MapPost("", async (
            CreateCustomerSegmentRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateSegmentAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/crm/segments/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        segments.MapPut("/{code}", async (
            string code,
            UpdateCustomerSegmentRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateSegmentAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        segments.MapPost("/{code}/activate", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetSegmentActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        segments.MapPost("/{code}/deactivate", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetSegmentActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);
    }

    private static void MapGuestEndpoints(RouteGroupBuilder api)
    {
        var guests = api.MapGroup("/crm/guests")
            .WithTags("CRM - Clients");

        guests.MapGet("", async (
            string? search,
            string? segmentCode,
            bool? vipOnly,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListGuestProfilesAsync(search, segmentCode, vipOnly == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        guests.MapGet("/{customerCode}", async (
            string customerCode,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetGuestProfileAsync(customerCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        // The 360 view answers "what is live TODAY" (running campaigns, upcoming stays). The day
        // is taken from the caller so a desktop client in Algiers is not told what is live in UTC.
        guests.MapGet("/{customerCode}/360", async (
            string customerCode,
            DateOnly? today,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomer360Async(
                customerCode,
                today ?? DateOnly.FromDateTime(DateTime.UtcNow),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        guests.MapPut("/{customerCode}", async (
            string customerCode,
            SaveGuestProfileRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SaveGuestProfileAsync(customerCode, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        guests.MapPost("/{customerCode}/marketing-consent", async (
            string customerCode,
            SetMarketingConsentRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetMarketingConsentAsync(customerCode, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);
    }

    private static void MapLoyaltyEndpoints(RouteGroupBuilder api)
    {
        var tiers = api.MapGroup("/crm/loyalty/tiers")
            .WithTags("CRM - Fidélité");

        tiers.MapGet("", async (
            bool? includeInactive,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListLoyaltyTiersAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        tiers.MapPost("", async (
            CreateLoyaltyTierRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateLoyaltyTierAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/crm/loyalty/tiers/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        tiers.MapPut("/{code}", async (
            string code,
            UpdateLoyaltyTierRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateLoyaltyTierAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        tiers.MapPost("/{code}/activate", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetLoyaltyTierActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        tiers.MapPost("/{code}/deactivate", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetLoyaltyTierActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        var accounts = api.MapGroup("/crm/loyalty/accounts")
            .WithTags("CRM - Fidélité");

        accounts.MapGet("/{customerCode}", async (
            string customerCode,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetLoyaltyStatementAsync(customerCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        MapLoyaltyMovement(accounts, "earn", LoyaltyTransactionKind.Earn);
        MapLoyaltyMovement(accounts, "redeem", LoyaltyTransactionKind.Redeem);
        MapLoyaltyMovement(accounts, "expire", LoyaltyTransactionKind.Expiry);
        MapLoyaltyMovement(accounts, "adjust", LoyaltyTransactionKind.Adjustment);
    }

    private static void MapLoyaltyMovement(
        RouteGroupBuilder accounts,
        string verb,
        LoyaltyTransactionKind kind)
    {
        accounts.MapPost($"/{{customerCode}}/{verb}", async (
            string customerCode,
            LoyaltyMovementRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RecordLoyaltyMovementAsync(
                customerCode,
                kind,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmLoyalty);
    }

    private static void MapCampaignEndpoints(RouteGroupBuilder api)
    {
        var campaigns = api.MapGroup("/crm/campaigns")
            .WithTags("CRM - Campagnes");

        campaigns.MapGet("", async (
            string? status,
            string? segmentCode,
            DateOnly? from,
            DateOnly? to,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseEnum<CampaignStatus>(status, "campaign status", out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListCampaignsAsync(parsedStatus, segmentCode, from, to, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        campaigns.MapGet("/{code}", async (
            string code,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCampaignAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        campaigns.MapGet("/{code}/audience", async (
            string code,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCampaignAudienceAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        campaigns.MapPost("", async (
            CreateCampaignRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCampaignAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/crm/campaigns/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        campaigns.MapPut("/{code}", async (
            string code,
            UpdateCampaignRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCampaignAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        campaigns.MapPost("/{code}/schedule", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ScheduleCampaignAsync(code, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        campaigns.MapPost("/{code}/launch", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.LaunchCampaignAsync(code, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        campaigns.MapPost("/{code}/complete", async (
            string code,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteCampaignAsync(code, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);

        campaigns.MapPost("/{code}/cancel", async (
            string code,
            CancelCampaignRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelCampaignAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);
    }

    private static void MapSatisfactionEndpoints(RouteGroupBuilder api)
    {
        var satisfaction = api.MapGroup("/crm/satisfaction")
            .WithTags("CRM - Satisfaction");

        satisfaction.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? customerCode,
            string? category,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseEnum<NpsCategory>(category, "NPS category", out var parsedCategory, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListSatisfactionEntriesAsync(
                from,
                to,
                hotelUnitCode,
                customerCode,
                parsedCategory,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        satisfaction.MapGet("/nps", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest(new ErrorResponse("The from and to dates are required."));
            }

            var result = await service.GetNpsSummaryAsync(from.Value, to.Value, hotelUnitCode, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        satisfaction.MapPost("", async (
            RecordSatisfactionRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RecordSatisfactionAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/crm/satisfaction/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);
    }

    private static void MapInteractionEndpoints(RouteGroupBuilder api)
    {
        var interactions = api.MapGroup("/crm/interactions")
            .WithTags("CRM - Contacts");

        interactions.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? customerCode,
            string? hotelUnitCode,
            ICrmService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.ListInteractionsAsync(from, to, customerCode, hotelUnitCode, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CrmRead);

        interactions.MapPost("", async (
            LogGuestInteractionRequest request,
            ICrmService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.LogInteractionAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/crm/interactions/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CrmWrite);
    }

    /// <summary>
    /// Parses an optional enum filter given as a query string. An unknown value is a caller
    /// mistake answered with a 400 naming the accepted values, not an empty result set that would
    /// read as "nothing matches".
    /// </summary>
    private static bool TryParseEnum<TEnum>(
        string? value,
        string what,
        out TEnum? parsed,
        out string error)
        where TEnum : struct, Enum
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var candidate) || !Enum.IsDefined(candidate))
        {
            error = $"Unknown {what}. Accepted values: {string.Join(", ", Enum.GetNames<TEnum>())}.";
            return false;
        }

        parsed = candidate;
        return true;
    }
}
