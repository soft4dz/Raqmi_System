using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Settings;

public sealed class ApplicationSettingsService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IApplicationSettingsService
{
    public async Task<ApplicationSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.Set<ApplicationSettings>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.SingletonKey == ApplicationSettings.SingletonKeyValue,
                cancellationToken);

        // Never a 404: an installation always runs with settings, configured or not. The row is
        // materialized by the first UpdateAsync rather than by a read - a GET is exercised by
        // every reader role (settings.read is granted to all of them, reader included) and must
        // stay side-effect free, which also keeps it working for a database opened read-only.
        return settings is null
            ? Map(ApplicationSettings.CreateDefault(), isConfigured: false)
            : Map(settings, isConfigured: true);
    }

    public async Task<ApplicationResult<ApplicationSettingsResponse>> UpdateAsync(
        UpdateApplicationSettingsRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.Set<ApplicationSettings>()
            .SingleOrDefaultAsync(
                current => current.SingletonKey == ApplicationSettings.SingletonKeyValue,
                cancellationToken);

        var isCreation = settings is null;
        var before = isCreation ? null : Map(settings!, isConfigured: true);

        settings ??= ApplicationSettings.CreateDefault();

        try
        {
            settings.UpdateCompanyIdentity(
                request.CompanyName,
                request.CompanyNif,
                request.CompanyRc,
                request.CompanyAi,
                request.CompanyNis,
                request.CompanyAddress,
                request.CompanyCity,
                request.CompanyPhone,
                request.CompanyEmail);

            settings.UpdateOperations(
                request.DefaultVatRate,
                request.CurrencyLabel,
                request.AuditRetentionDays);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ApplicationSettingsResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;

        if (isCreation)
        {
            settings.MarkCreated(context.UserName, now);
            dbContext.Set<ApplicationSettings>().Add(settings);
        }
        else
        {
            settings.MarkUpdated(context.UserName, now);
        }

        var after = Map(settings, isConfigured: true);

        try
        {
            await WriteAuditAsync(
                "settings.application.updated",
                settings.Id,
                context,
                new { Created = isCreation, Changes = DescribeChanges(before, after) },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The singleton is enforced by a unique index on singleton_key: a concurrent first
            // update lost the race and would otherwise insert a second row.
            return ApplicationResult<ApplicationSettingsResponse>.Conflict(
                "The settings were created by a concurrent operation. Please retry.");
        }

        return ApplicationResult<ApplicationSettingsResponse>.Success(after);
    }

    /// <summary>
    /// Field-level description of what the update actually changed, so the audit trail says more
    /// than "someone touched the settings". On the very first write there is nothing to compare
    /// against and the full resulting configuration is recorded instead.
    /// </summary>
    private static IReadOnlyCollection<string> DescribeChanges(
        ApplicationSettingsResponse? before,
        ApplicationSettingsResponse after)
    {
        if (before is null)
        {
            return new[]
            {
                DescribeInitial(nameof(after.CompanyName), after.CompanyName),
                DescribeInitial(nameof(after.CompanyNif), after.CompanyNif),
                DescribeInitial(nameof(after.DefaultVatRate), after.DefaultVatRate),
                DescribeInitial(nameof(after.CurrencyLabel), after.CurrencyLabel),
                DescribeInitial(nameof(after.AuditRetentionDays), after.AuditRetentionDays)
            };
        }

        var changes = new List<string>();

        AddIfChanged(changes, nameof(after.CompanyName), before.CompanyName, after.CompanyName);
        AddIfChanged(changes, nameof(after.CompanyNif), before.CompanyNif, after.CompanyNif);
        AddIfChanged(changes, nameof(after.CompanyRc), before.CompanyRc, after.CompanyRc);
        AddIfChanged(changes, nameof(after.CompanyAi), before.CompanyAi, after.CompanyAi);
        AddIfChanged(changes, nameof(after.CompanyNis), before.CompanyNis, after.CompanyNis);
        AddIfChanged(changes, nameof(after.CompanyAddress), before.CompanyAddress, after.CompanyAddress);
        AddIfChanged(changes, nameof(after.CompanyCity), before.CompanyCity, after.CompanyCity);
        AddIfChanged(changes, nameof(after.CompanyPhone), before.CompanyPhone, after.CompanyPhone);
        AddIfChanged(changes, nameof(after.CompanyEmail), before.CompanyEmail, after.CompanyEmail);
        AddIfChanged(changes, nameof(after.DefaultVatRate), before.DefaultVatRate, after.DefaultVatRate);
        AddIfChanged(changes, nameof(after.CurrencyLabel), before.CurrencyLabel, after.CurrencyLabel);
        AddIfChanged(changes, nameof(after.AuditRetentionDays), before.AuditRetentionDays, after.AuditRetentionDays);

        return changes;
    }

    private static void AddIfChanged<T>(List<string> changes, string field, T? before, T? after)
    {
        if (!Equals(before, after))
        {
            changes.Add(Describe(field, before, after));
        }
    }

    private static string Describe<T>(string field, T? before, T? after)
    {
        return $"{field}: '{before}' -> '{after}'";
    }

    private static string DescribeInitial<T>(string field, T? value)
    {
        return $"{field}: set to '{value}'";
    }

    private static ApplicationSettingsResponse Map(ApplicationSettings settings, bool isConfigured)
    {
        return new ApplicationSettingsResponse(
            settings.CompanyName,
            settings.CompanyNif,
            settings.CompanyRc,
            settings.CompanyAi,
            settings.CompanyNis,
            settings.CompanyAddress,
            settings.CompanyCity,
            settings.CompanyPhone,
            settings.CompanyEmail,
            settings.DefaultVatRate,
            settings.CurrencyLabel,
            settings.AuditRetentionDays,
            isConfigured,
            settings.CreatedAt,
            settings.CreatedBy,
            settings.UpdatedAt,
            settings.UpdatedBy);
    }

    /// <summary>
    /// Explicit flush after the audit write, mirroring BillingService: AuditLogWriter.WriteAsync
    /// already saves, so this call is usually a no-op - it exists so persistence never silently
    /// depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                "settings.application_settings",
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
