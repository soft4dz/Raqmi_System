using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Api.Endpoints;

internal static class HttpResultExtensions
{
    public static IResult ToHttpResult<T>(this ApplicationResult<T> result)
    {
        if (result.Succeeded && result.Value is not null)
        {
            return Results.Ok(result.Value);
        }

        return result.ErrorType switch
        {
            ApplicationErrorType.NotFound => Results.NotFound(new ErrorResponse(result.Error ?? "Resource was not found.")),
            ApplicationErrorType.Conflict => Results.Conflict(new ErrorResponse(result.Error ?? "Resource conflict.")),
            ApplicationErrorType.Validation => Results.BadRequest(new ErrorResponse(result.Error ?? "Validation failed.")),
            _ => Results.Problem(result.Error ?? "Unexpected application error.")
        };
    }
}
