namespace RaqmiSystem.Application.Security;

public sealed record OperationContext(
    Guid? UserId,
    string UserName,
    string? IpAddress)
{
    public static OperationContext System { get; } = new(null, "system", null);
}
