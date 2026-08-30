using RaqmiSystem.Application.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module "Mon compte": what an authenticated user does to their own account, as opposed to
/// <see cref="SecurityEndpoints"/>, which is what an administrator does to someone else's.
///
/// It is protected by a bare <c>RequireAuthorization()</c> - no permission key, and
/// none is added to <c>PermissionCatalog</c>. A permission is the wrong tool here: it would have to
/// be granted to every role to be usable, at which point it grants nothing, and forgetting it on one
/// role would leave that role's users unable to ever leave the temporary password an administrator
/// handed them. Holding a valid token for the account IS the authorization, because the account
/// acted upon is not chosen by the caller.
/// </summary>
internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var account = api.MapGroup("/account")
            .WithTags("Account");

        // POST, not PUT: this is not an idempotent overwrite of a resource. It re-authenticates the
        // caller and, as a side effect, closes their other sessions - replaying it is not a no-op.
        account.MapPost("/change-password", async (
            ChangePasswordRequest request,
            IAccountService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var context = httpContext.ToOperationContext();

            // The target account is read from the token, never from the body. An authenticated
            // principal whose token carries no usable subject claim cannot identify itself, so
            // there is no account to act on - and guessing one from the body is exactly what this
            // endpoint must never do.
            if (context.UserId is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                return Results.BadRequest(new ErrorResponse("The currentPassword field is required."));
            }

            if (string.IsNullOrEmpty(request.NewPassword))
            {
                return Results.BadRequest(new ErrorResponse("The newPassword field is required."));
            }

            // Every rule - current password correct, length, difference, session revocation - is
            // the service's; the endpoint only proves who is asking.
            var result = await service.ChangePasswordAsync(
                context.UserId.Value,
                request.CurrentPassword,
                request.NewPassword,
                context,
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization();

        return api;
    }
}
