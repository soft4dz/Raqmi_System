using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

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
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ISecuritySeeder, SecuritySeeder>();

        return services;
    }
}
