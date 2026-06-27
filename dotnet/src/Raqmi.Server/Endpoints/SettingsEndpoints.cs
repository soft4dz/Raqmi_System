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
            if (!IsAdmin(http))
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

    private static bool IsAdmin(HttpContext http) =>
        string.Equals(http.User.FindFirst("roleCode")?.Value, "admin", StringComparison.OrdinalIgnoreCase);

    private static void ApplyPatch(TenantSettings settings, SettingsPatchRequest body)
    {
        if (body.LegalName is not null) settings.LegalName = body.LegalName.Trim();
        if (body.Email is not null) settings.Email = body.Email.Trim();
        if (body.Phone is not null) settings.Phone = body.Phone.Trim();
        if (body.Address is not null) settings.Address = body.Address.Trim();
        if (body.Currency is not null) settings.Currency = body.Currency.Trim().ToUpperInvariant();
        if (body.DateFormat is not null) settings.DateFormat = body.DateFormat.Trim();
        if (body.NumberFormat is not null) settings.NumberFormat = body.NumberFormat.Trim();
        if (body.Timezone is not null) settings.Timezone = body.Timezone.Trim();
        if (body.PaymentDelayDays.HasValue) settings.PaymentDelayDays = body.PaymentDelayDays.Value;
        if (body.ReminderDelayDays.HasValue) settings.ReminderDelayDays = body.ReminderDelayDays.Value;
        if (body.BrandPrimaryColor is not null) settings.BrandPrimaryColor = body.BrandPrimaryColor.Trim();
        if (body.BrandLogoUrl is not null) settings.BrandLogoUrl = string.IsNullOrWhiteSpace(body.BrandLogoUrl) ? null : body.BrandLogoUrl.Trim();
    }

    private static object ToDto(TenantSettings s) => new
    {
        legalName = s.LegalName,
        email = s.Email,
        phone = s.Phone,
        address = s.Address,
        currency = s.Currency,
        dateFormat = s.DateFormat,
        numberFormat = s.NumberFormat,
        timezone = s.Timezone,
        paymentDelayDays = s.PaymentDelayDays,
        reminderDelayDays = s.ReminderDelayDays,
        brandPrimaryColor = s.BrandPrimaryColor,
        brandLogoUrl = s.BrandLogoUrl,
    };
}
