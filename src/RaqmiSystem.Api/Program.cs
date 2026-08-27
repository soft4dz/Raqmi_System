using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PostgresOptions>(
    builder.Configuration.GetSection(PostgresOptions.SectionName));

builder.Services.AddScoped<RevenueSummaryService>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    application = "Raqmi System",
    version = "1.0.0-alpha"
}));

app.MapGet("/api/v1/me", () =>
{
    var context = new UserContextDto(
        "demo.admin",
        "Administrateur Raqmi",
        ["Admin"],
        ["users.read", "revenue.read", "revenue.write", "audit.read"]);

    return Results.Ok(context);
});

app.MapGet("/api/v1/revenue/sample-summary", (RevenueSummaryService service) =>
{
    var sample = new[]
    {
        new DailyRevenueDraft(DateOnly.FromDateTime(DateTime.Today), "EL-MANAR", 1200000m, 340000m, 110000m, 80000m),
        new DailyRevenueDraft(DateOnly.FromDateTime(DateTime.Today), "EL-MARSA", 900000m, 280000m, 95000m, 60000m)
    };

    return Results.Ok(service.Calculate(sample));
});

app.Run();

public partial class Program
{
}
