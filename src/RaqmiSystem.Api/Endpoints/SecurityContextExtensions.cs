using System.Security.Claims;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Api.Endpoints;

internal static class SecurityContextExtensions
{
    public static OperationContext ToOperationContext(this HttpContext httpContext)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : (Guid?)null;
        var userName = httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? "unknown";

        return new OperationContext(
            userId,
            userName,
            httpContext.Connection.RemoteIpAddress?.ToString());
    }
}
