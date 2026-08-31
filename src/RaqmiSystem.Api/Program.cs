using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RaqmiSystem.Api.Endpoints;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (B9): replaces the default ASP.NET Core console logger with
// Serilog, writing one compact JSON object per line to stdout. That is what
// docker-compose.prod.yml (and any external log aggregator reading container
// stdout) expects, instead of the default human-oriented text format.
builder.Host.UseSerilog((_, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter());
});

builder.Configuration.AddEnvironmentVariables(prefix: "RAQMI_");

var jwtOptions = JwtOptions.FromConfiguration(
    builder.Configuration,
    allowEphemeralDevelopmentKey: builder.Environment.IsDevelopment());

builder.Services.AddRaqmiInfrastructure(builder.Configuration, jwtOptions);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionCatalog.All)
    {
        options.AddPolicy(permission.Key, policy => policy.RequireClaim(SecurityClaimTypes.Permission, permission.Key));
    }
});

var app = builder.Build();

if (args.Contains("--seed-security", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ISecuritySeeder>();
    await seeder.SeedAsync(CancellationToken.None);
    return;
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    application = "Raqmi System",
    version = "1.0.0-alpha"
}));

app.MapGet("/health/database", async (RaqmiDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "healthy", database = "postgresql" })
        : Results.Problem("PostgreSQL is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

var api = app.MapGroup("/api/v1");

api.MapPost("/auth/login", async (
    LoginRequest request,
    IAuthenticationService authenticationService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await authenticationService.SignInAsync(
        request,
        httpContext.Connection.RemoteIpAddress?.ToString(),
        cancellationToken);

    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

api.MapPost("/auth/refresh", async (
    RefreshTokenRequest request,
    IAuthenticationService authenticationService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var result = await authenticationService.RefreshAsync(
        request.RefreshToken,
        httpContext.Connection.RemoteIpAddress?.ToString(),
        cancellationToken);

    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

api.MapGet("/me", (ClaimsPrincipal user) =>
{
    var permissions = user.Claims
        .Where(claim => claim.Type == SecurityClaimTypes.Permission)
        .Select(claim => claim.Value)
        .Order()
        .ToArray();

    var roles = user.Claims
        .Where(claim => claim.Type == ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Order()
        .ToArray();

    return Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        userName = user.Identity?.Name,
        email = user.FindFirstValue(ClaimTypes.Email),
        roles,
        permissions
    });
}).RequireAuthorization();

api.MapSecurityEndpoints();
api.MapOrganizationEndpoints();
api.MapRevenueEndpoints();
api.MapAuditEndpoints();
api.MapClosingEndpoints();
api.MapTreasuryEndpoints();
api.MapBillingEndpoints();
api.MapTariffsEndpoints();
api.MapLodgingEndpoints();
api.MapSettingsEndpoints();
api.MapAccountEndpoints();
api.MapAccountingEndpoints();
api.MapBudgetEndpoints();
api.MapReceivablesEndpoints();
api.MapApprovalsEndpoints();
api.MapReportingEndpoints();
api.MapMaintenanceEndpoints();

app.Run();

public partial class Program
{
}
