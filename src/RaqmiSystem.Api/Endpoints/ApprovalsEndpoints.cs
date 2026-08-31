using System.Security.Claims;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Workflows &amp; validations (module 22.2): configurable approval circuits, in-flight approval
/// instances, and the decide actions.
///
/// NOTE FOR THE INTEGRATOR - the three permission keys below are deliberately LITERAL strings
/// ("approvals.read", "approvals.write", "approvals.decide"). Replace them with the
/// PermissionCatalog constants once the keys are added to the catalog (Program.cs only registers
/// an authorization policy per PermissionCatalog.All entry, so the keys MUST be added there for
/// these routes to resolve their policies).
/// </summary>
internal static class ApprovalsEndpoints
{
    public static RouteGroupBuilder MapApprovalsEndpoints(this RouteGroupBuilder api)
    {
        MapCircuitEndpoints(api);
        MapInstanceEndpoints(api);
        return api;
    }

    private static void MapCircuitEndpoints(RouteGroupBuilder api)
    {
        var circuits = api.MapGroup("/approvals/circuits")
            .WithTags("Approval circuits");

        circuits.MapGet("", async (
            string? subjectType,
            bool? includeInactive,
            IApprovalService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseSubjectType(subjectType, out var parsedSubjectType, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListCircuitsAsync(parsedSubjectType, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.ApprovalsRead);

        circuits.MapGet("/{code}", async (
            string code,
            IApprovalService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCircuitAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsRead);

        circuits.MapPost("", async (
            CreateApprovalCircuitRequest request,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCircuitAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/approvals/circuits/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsWrite);

        circuits.MapPut("/{code}", async (
            string code,
            UpdateApprovalCircuitRequest request,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCircuitAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsWrite);

        circuits.MapPost("/{code}/activate", async (
            string code,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCircuitActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsWrite);

        circuits.MapPost("/{code}/deactivate", async (
            string code,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCircuitActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsWrite);
    }

    private static void MapInstanceEndpoints(RouteGroupBuilder api)
    {
        var instances = api.MapGroup("/approvals/instances")
            .WithTags("Approval instances");

        instances.MapGet("", async (
            string? subjectType,
            string? subjectReference,
            string? status,
            DateOnly? from,
            DateOnly? to,
            IApprovalService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseSubjectType(subjectType, out var parsedSubjectType, out var subjectError))
            {
                return Results.BadRequest(new ErrorResponse(subjectError));
            }

            if (!TryParseStatus(status, out var parsedStatus, out var statusError))
            {
                return Results.BadRequest(new ErrorResponse(statusError));
            }

            var result = await service.ListInstancesAsync(
                parsedSubjectType,
                subjectReference,
                parsedStatus,
                from,
                to,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.ApprovalsRead);

        // "My pending decisions": the work queue of the authenticated decider. Filtered on the
        // caller's ROLE claims because a circuit step demands a role, not a permission - holding
        // approvals.decide says you may decide in general, your roles say which steps are yours.
        instances.MapGet("/pending", async (
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPendingAsync(GetRoleClaims(httpContext), cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.ApprovalsDecide);

        instances.MapGet("/{id:guid}", async (
            Guid id,
            IApprovalService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetInstanceAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsRead);

        instances.MapPost("", async (
            OpenApprovalInstanceRequest request,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.OpenInstanceAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/approvals/instances/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsWrite);

        instances.MapPost("/{id:guid}/approve", async (
            Guid id,
            DecideApprovalRequest request,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DecideAsync(
                id,
                approved: true,
                request.Comment,
                GetRoleClaims(httpContext),
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsDecide);

        instances.MapPost("/{id:guid}/reject", async (
            Guid id,
            DecideApprovalRequest request,
            IApprovalService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DecideAsync(
                id,
                approved: false,
                request.Comment,
                GetRoleClaims(httpContext),
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ApprovalsDecide);
    }

    /// <summary>
    /// The decider's system roles, straight from the authenticated principal's role claims
    /// (minted by JwtTokenService from the user's role assignments). The service matches them
    /// against the role required by the instance's current step.
    /// </summary>
    private static string[] GetRoleClaims(HttpContext httpContext)
    {
        return httpContext.User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryParseSubjectType(string? subjectType, out ApprovalSubjectType? parsed, out string error)
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(subjectType))
        {
            return true;
        }

        if (Enum.TryParse<ApprovalSubjectType>(subjectType.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsed = value;
            return true;
        }

        error = "Approval subject type must be PaymentOrder.";
        return false;
    }

    private static bool TryParseStatus(string? status, out ApprovalInstanceStatus? parsed, out string error)
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<ApprovalInstanceStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsed = value;
            return true;
        }

        error = "Approval instance status must be InProgress, Approved or Rejected.";
        return false;
    }
}
