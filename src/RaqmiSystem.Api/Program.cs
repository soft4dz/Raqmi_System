using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "RAQMI_");

var jwtOptions = JwtOptions.FromConfiguration(
    builder.Configuration,
    allowEphemeralDevelopmentKey: builder.Environment.IsDevelopment());

builder.Services.AddRaqmiInfrastructure(builder.Configuration, jwtOptions);

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

builder.Services.AddScoped<RevenueSummaryService>();

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

api.MapGet("/security/permissions", () =>
{
    var permissions = PermissionCatalog.All
        .Select(permission => new PermissionSummary(
            permission.Key,
            permission.Name,
            permission.Category,
            permission.Description))
        .OrderBy(permission => permission.Category)
        .ThenBy(permission => permission.Key)
        .ToArray();

    return Results.Ok(permissions);
}).RequireAuthorization(PermissionCatalog.UsersRead);

api.MapGet("/security/users", async (RaqmiDbContext db, CancellationToken cancellationToken) =>
{
    var users = await db.Users
        .AsNoTracking()
        .OrderBy(user => user.UserName)
        .Select(user => new UserSummary(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword))
        .ToArrayAsync(cancellationToken);

    return Results.Ok(users);
}).RequireAuthorization(PermissionCatalog.UsersRead);

api.MapGet("/revenue/sample-summary", (RevenueSummaryService service) =>
{
    var sample = new[]
    {
        new DailyRevenueDraft(DateOnly.FromDateTime(DateTime.Today), "EL-MANAR", 1200000m, 340000m, 110000m, 80000m),
        new DailyRevenueDraft(DateOnly.FromDateTime(DateTime.Today), "EL-MARSA", 900000m, 280000m, 95000m, 60000m)
    };

    return Results.Ok(service.Calculate(sample));
}).RequireAuthorization(PermissionCatalog.RevenueRead);

app.Run();

public partial class Program
{
}
