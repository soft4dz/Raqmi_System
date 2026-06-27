namespace Raqmi.Server.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            service = "raqmi-system-server",
            version = "0.1.0",
            mode = runtime.DemoMode ? "demo" : "database",
            storage = runtime.StorageDriver,
            timestamp = DateTimeOffset.UtcNow,
        }, JsonDefaults.Options));
    }
}
