using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Application.Closing;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Sync;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Infrastructure.Accounting;
using RaqmiSystem.Infrastructure.Approvals;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Budgeting;
using RaqmiSystem.Infrastructure.Closing;
using RaqmiSystem.Infrastructure.Crm;
using RaqmiSystem.Infrastructure.Housekeeping;
using RaqmiSystem.Infrastructure.Identity;
using RaqmiSystem.Infrastructure.Inventory;
using RaqmiSystem.Infrastructure.Kitchen;
using RaqmiSystem.Infrastructure.HumanResources;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Maintenance;
using RaqmiSystem.Infrastructure.Mice;
using RaqmiSystem.Infrastructure.Sync;
using RaqmiSystem.Infrastructure.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Purchasing;
using RaqmiSystem.Infrastructure.Pilotage;
using RaqmiSystem.Infrastructure.Receivables;
using RaqmiSystem.Infrastructure.Reporting;
using RaqmiSystem.Infrastructure.Revenue;
using RaqmiSystem.Infrastructure.Security;
using RaqmiSystem.Infrastructure.Settings;
using RaqmiSystem.Infrastructure.Tariffs;
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
        services.AddScoped<ITariffService, TariffService>();
        services.AddScoped<ITariffResolutionService, TariffResolutionService>();
        services.AddScoped<ILodgingService, LodgingService>();

        // Housekeeping depends on ILodgingService so a minibar consumption and the folio
        // line it bills are written by the SAME folio code path, inside its transaction -
        // rather than re-implementing the checked-in guard here where it could drift.
        services.AddScoped<IHousekeepingService, HousekeepingService>();
        // The CRM reads the customer file through the module that owns it (IBillingService)
        // rather than re-projecting the customers table: the 360 view must show exactly
        // what the customer screen shows.
        services.AddScoped<ICrmService, CrmService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IApprovalGate, ApprovalService>();
        services.AddScoped<IHumanResourcesService, HumanResourcesService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IBackupService, BackupService>();

        // Module Pilotage : deux lecteurs d'agregation pure (aucune table, aucune ecriture).
        services.AddScoped<IGroupDashboardService, GroupDashboardService>();
        services.AddScoped<IDecCockpitService, DecCockpitService>();

        // Vague E1 : la chaine stocks -> achats -> cuisine.
        // InventoryService porte TROIS contrats : son propre IInventoryService et les deux
        // contrats publies vers les autres modules (IStockOperationService, consomme par les
        // receptions d'achat ; IStockCostProvider, consomme par le cout matiere des fiches
        // techniques et par le controle d'existence d'un article commande). Les trois
        // enregistrements donnent trois instances par scope, ce qui est sans effet ici :
        // tout l'etat vit dans le RaqmiDbContext scoped partage. Meme precedent que
        // IDailyClosingService / IDailyClosingReadService plus haut.
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockOperationService, InventoryService>();
        services.AddScoped<IStockCostProvider, InventoryService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<IKitchenService, KitchenService>();

        // Module 29 : supervision des postes. Lecture seule cote metier - ce service ne touche
        // aucune donnee d'exploitation, il tient un inventaire des clients deployes.
        services.AddScoped<ISyncSupervisionService, SyncSupervisionService>();

        // Module 10.6 : l'evenementiel facture PAR le module Facturation (IBillingService)
        // plutot que par une seconde implementation - une facture d'evenement doit etre de la
        // meme nature que toutes les autres. Meme precedent que Housekeeping -> ILodgingService.
        services.AddScoped<IMiceService, MiceService>();

        return services;
    }
}
