using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder api)
    {
        var audit = api.MapGroup("/audit")
            .WithTags("Audit");

        audit.MapGet("", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            Guid? userId,
            string? action,
            int? page,
            int? pageSize,
            IAuditQueryService service,
            CancellationToken cancellationToken) =>
        {
            var resolvedPage = page ?? 1;
            var resolvedPageSize = pageSize ?? 50;

            if (resolvedPage < 1)
            {
                return Results.BadRequest(new ErrorResponse("Page must be a positive number."));
            }

            if (resolvedPageSize < 1)
            {
                return Results.BadRequest(new ErrorResponse("Page size must be a positive number."));
            }

            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.SearchAsync(from, to, userId, action, resolvedPage, resolvedPageSize, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.AuditRead);

        // Purge is a data-destructive, retention-policy operation, so it is gated behind the same
        // permission as the security seed command rather than the read-only audit.read.
        audit.MapPost("/purge", async (
            int? olderThanDays,
            IAuditQueryService service,
            CancellationToken cancellationToken) =>
        {
            if (olderThanDays is null || olderThanDays < 1)
            {
                return Results.BadRequest(new ErrorResponse("olderThanDays must be a positive number."));
            }

            var threshold = DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value);
            var deletedCount = await service.PurgeOlderThanAsync(threshold, cancellationToken);

            return Results.Ok(new AuditPurgeResponse(deletedCount, threshold));
        }).RequireAuthorization(PermissionCatalog.SecuritySeed);

        return api;
    }
}
