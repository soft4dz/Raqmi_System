using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Licensing;
using Raqmi.Server;
using Raqmi.Server.Endpoints;
using Raqmi.Shared;

var builder = WebApplication.CreateBuilder(args);

var port = int.TryParse(builder.Configuration["PORT"], out var parsedPort) ? parsedPort : 3000;
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var connectionString = builder.Configuration.GetConnectionString("Default");
var useDatabase = !string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddSingleton<LicenseStore>();

var demoMode = builder.Configuration.GetValue("DEMO_MODE", true)
    || (!useDatabase && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")));

var runtime = new ServerRuntime
{
    DemoMode = demoMode,
    UseDatabase = useDatabase,
    LicenseMode = builder.Configuration["LICENSE_MODE"] ?? "offline",
    StorageDriver = builder.Configuration["FILE_STORAGE_DRIVER"] ?? "local",
    Port = port,
};
builder.Services.AddSingleton(runtime);

if (useDatabase)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddDbContext<RaqmiDbContext>(options =>
    {
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connectionString);
        else
            options.UseNpgsql(connectionString);
    });
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["CLIENT_ORIGIN"] ?? "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

if (useDatabase)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
    if (connectionString!.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await RaqmiDbSeeder.SeedAsync(db);
}

app.MapHealthEndpoints(runtime);
app.MapAuthEndpoints(runtime);
app.MapTenantEndpoints(runtime);
app.MapLicenseEndpoints(runtime);
app.MapModuleEndpoints(runtime);
app.MapSiteEndpoints(runtime, app.Services.GetRequiredService<LicenseStore>());
app.MapSettingsEndpoints(runtime);
app.MapAdminEndpoints(runtime);
app.MapFinanceEndpoints(runtime);
app.MapHrEndpoints(runtime);
app.MapStockEndpoints(runtime);
app.MapGedEndpoints(runtime);

if (demoMode)
{
    var enabled = ModuleEntitlement.CountEnabledModules(DemoData.License.AllowedModules);
    Console.WriteLine($"[demo] Pack Professional: {enabled}/{ModuleCatalog.All.Count} modules actifs");
}

Console.WriteLine($"Raqmi System Server → http://127.0.0.1:{port}");
Console.WriteLine($"Mode: {(demoMode ? "demo" : "production")} | Base: {(useDatabase ? "PostgreSQL" : "sans base")}");

app.Run();

public sealed record LoginRequest(string? Email, string? Password);
public sealed record ImportLicenseRequest(string? Content);

public partial class Program;
