using System.Net;

namespace RaqmiSystem.Desktop.Api;

public sealed class ApiRequestFailedException : Exception
{
    public ApiRequestFailedException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
