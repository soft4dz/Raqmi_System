namespace Raqmi.Server;

public sealed class ServerRuntime
{
    public required bool DemoMode { get; init; }
    public required bool UseDatabase { get; init; }
    public required string LicenseMode { get; init; }
    public required string StorageDriver { get; init; }
    public required int Port { get; init; }
}
