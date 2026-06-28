using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        var group = app.MapGroup("/api/v1/settings");

        group.MapGet("/", async (HttpContext http) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();

            if (runtime.UseDatabase && db is not null)
            {
                var settings = await db.Set<TenantSettings>().FirstOrDefaultAsync(x => x.TenantId == tenantId);
                if (settings is null)
                {
                    var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId);
                    settings = new TenantSettings
                    {
                        TenantId = tenantId,
                        LegalName = tenant?.Name,
                        Email = tenant?.Email,
                    };
                    db.Set<TenantSettings>().Add(settings);
                    await db.SaveChangesAsync();
                }

                return Results.Json(ToDto(settings), JsonDefaults.Options);
            }

            if (runtime.DemoMode)
                return Results.Json(DemoSettingsStore.Get(), JsonDefaults.Options);

            return Results.StatusCode(501);
        }).RequireRaqmiModule("settings");

        group.MapPatch("/", async (HttpContext http, SettingsPatchRequest body) =>
        {
            if (!EndpointAuth.CanWriteSettings(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();

            if (runtime.UseDatabase && db is not null)
            {
                var settings = await db.Set<TenantSettings>().FirstOrDefaultAsync(x => x.TenantId == tenantId)
                    ?? new TenantSettings { TenantId = tenantId };

                ApplyPatch(settings, body);
                settings.UpdatedAt = DateTime.UtcNow;

                if (db.Entry(settings).State == EntityState.Detached)
                    db.Set<TenantSettings>().Add(settings);
                await db.SaveChangesAsync();
                return Results.Json(ToDto(settings), JsonDefaults.Options);
            }

            if (runtime.DemoMode)
                return Results.Json(DemoSettingsStore.Patch(body), JsonDefaults.Options);

            return Results.StatusCode(501);
        }).RequireRaqmiModule("settings");
    }

    private static void ApplyPatch(TenantSettings settings, SettingsPatchRequest body)
    {
        if (body.LegalName is not null) settings.LegalName = body.LegalName.Trim();
        if (body.TradeName is not null) settings.TradeName = body.TradeName.Trim();
        if (body.LegalForm is not null) settings.LegalForm = body.LegalForm.Trim();
        if (body.RegistrationNumber is not null) settings.RegistrationNumber = body.RegistrationNumber.Trim();
        if (body.TaxId is not null) settings.TaxId = body.TaxId.Trim();
        if (body.StatisticalId is not null) settings.StatisticalId = body.StatisticalId.Trim();
        if (body.VatArticle is not null) settings.VatArticle = body.VatArticle.Trim();
        if (body.ActivitySector is not null) settings.ActivitySector = body.ActivitySector.Trim();
        if (body.Email is not null) settings.Email = body.Email.Trim();
        if (body.Phone is not null) settings.Phone = body.Phone.Trim();
        if (body.Website is not null) settings.Website = body.Website.Trim();
        if (body.Address is not null) settings.Address = body.Address.Trim();
        if (body.City is not null) settings.City = body.City.Trim();
        if (body.Wilaya is not null) settings.Wilaya = body.Wilaya.Trim();
        if (body.PostalCode is not null) settings.PostalCode = body.PostalCode.Trim();
        if (body.Country is not null) settings.Country = body.Country.Trim();
        if (body.Currency is not null) settings.Currency = body.Currency.Trim().ToUpperInvariant();
        if (body.DateFormat is not null) settings.DateFormat = body.DateFormat.Trim();
        if (body.NumberFormat is not null) settings.NumberFormat = body.NumberFormat.Trim();
        if (body.Timezone is not null) settings.Timezone = body.Timezone.Trim();
        if (body.PaymentDelayDays.HasValue) settings.PaymentDelayDays = body.PaymentDelayDays.Value;
        if (body.ReminderDelayDays.HasValue) settings.ReminderDelayDays = body.ReminderDelayDays.Value;
        if (body.InvoicePrefix is not null) settings.InvoicePrefix = body.InvoicePrefix.Trim();
        if (body.QuotePrefix is not null) settings.QuotePrefix = body.QuotePrefix.Trim();
        if (body.NextInvoiceNumber.HasValue) settings.NextInvoiceNumber = body.NextInvoiceNumber.Value;
        if (body.FiscalYearStartMonth.HasValue) settings.FiscalYearStartMonth = body.FiscalYearStartMonth.Value;
        if (body.DefaultVatRate.HasValue) settings.DefaultVatRate = body.DefaultVatRate.Value;
        if (body.InvoiceFooter is not null) settings.InvoiceFooter = body.InvoiceFooter.Trim();
        if (body.AcceptedPaymentMethods is not null) settings.AcceptedPaymentMethods = body.AcceptedPaymentMethods.Trim();
        if (body.BrandPrimaryColor is not null) settings.BrandPrimaryColor = body.BrandPrimaryColor.Trim();
        if (body.BrandSecondaryColor is not null) settings.BrandSecondaryColor = body.BrandSecondaryColor.Trim();
        if (body.BrandLogoUrl is not null) settings.BrandLogoUrl = string.IsNullOrWhiteSpace(body.BrandLogoUrl) ? null : body.BrandLogoUrl.Trim();
        if (body.SessionTimeoutMinutes.HasValue) settings.SessionTimeoutMinutes = body.SessionTimeoutMinutes.Value;
        if (body.PasswordMinLength.HasValue) settings.PasswordMinLength = body.PasswordMinLength.Value;
        if (body.ForcePasswordChangeDays.HasValue) settings.ForcePasswordChangeDays = body.ForcePasswordChangeDays.Value;
        if (body.StorageDriver is not null) settings.StorageDriver = body.StorageDriver.Trim();
        if (body.MaxUploadSizeMb.HasValue) settings.MaxUploadSizeMb = body.MaxUploadSizeMb.Value;
        if (body.BackupFrequency is not null) settings.BackupFrequency = body.BackupFrequency.Trim();
        if (body.BackupRetentionDays.HasValue) settings.BackupRetentionDays = body.BackupRetentionDays.Value;
    }

    private static object ToDto(TenantSettings s) => new
    {
        legalName = s.LegalName,
        tradeName = s.TradeName,
        legalForm = s.LegalForm,
        registrationNumber = s.RegistrationNumber,
        taxId = s.TaxId,
        statisticalId = s.StatisticalId,
        vatArticle = s.VatArticle,
        activitySector = s.ActivitySector,
        email = s.Email,
        phone = s.Phone,
        website = s.Website,
        address = s.Address,
        city = s.City,
        wilaya = s.Wilaya,
        postalCode = s.PostalCode,
        country = s.Country,
        currency = s.Currency,
        dateFormat = s.DateFormat,
        numberFormat = s.NumberFormat,
        timezone = s.Timezone,
        paymentDelayDays = s.PaymentDelayDays,
        reminderDelayDays = s.ReminderDelayDays,
        invoicePrefix = s.InvoicePrefix,
        quotePrefix = s.QuotePrefix,
        nextInvoiceNumber = s.NextInvoiceNumber,
        fiscalYearStartMonth = s.FiscalYearStartMonth,
        defaultVatRate = s.DefaultVatRate,
        invoiceFooter = s.InvoiceFooter,
        acceptedPaymentMethods = s.AcceptedPaymentMethods,
        brandPrimaryColor = s.BrandPrimaryColor,
        brandSecondaryColor = s.BrandSecondaryColor,
        brandLogoUrl = s.BrandLogoUrl,
        sessionTimeoutMinutes = s.SessionTimeoutMinutes,
        passwordMinLength = s.PasswordMinLength,
        forcePasswordChangeDays = s.ForcePasswordChangeDays,
        storageDriver = s.StorageDriver,
        maxUploadSizeMb = s.MaxUploadSizeMb,
        backupFrequency = s.BackupFrequency,
        backupRetentionDays = s.BackupRetentionDays,
        updatedAt = s.UpdatedAt,
    };
}
