using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Application.Closing;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Infrastructure.Accounting;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Budgeting;
using RaqmiSystem.Infrastructure.Closing;
using RaqmiSystem.Infrastructure.Identity;
using RaqmiSystem.Infrastructure.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Receivables;
using RaqmiSystem.Infrastructure.Revenue;
using RaqmiSystem.Infrastructure.Security;
using RaqmiSystem.Infrastructure.Settings;
using RaqmiSystem.Infrastructure.Treasury;

namespace RaqmiSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRaqmiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        JwtOptions jwtOptions)
    {
        var postgresOptions = PostgresOptions.FromConfiguration(configuration);

        services.AddSingleton(Options.Create(postgresOptions));
        services.AddSingleton(Options.Create(jwtOptions));

        services.AddDbContext<RaqmiDbContext>(options =>
        {
            options.UseNpgsql(ConnectionStringFactory.Build(postgresOptions));
        });

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<ISecuritySeeder, SecuritySeeder>();
        services.AddScoped<IHotelUnitService, HotelUnitService>();
        services.AddScoped<IDailyRevenueService, DailyRevenueService>();
        services.AddScoped<IDailyClosingService, DailyClosingService>();
        services.AddScoped<IDailyClosingReadService, DailyClosingService>();
        services.AddScoped<ITreasuryService, TreasuryService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IReceivablesService, ReceivablesService>();

        return services;
    }
}
