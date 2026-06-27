namespace Raqmi.Server;

public static class DemoSettingsStore
{
    private sealed class State
    {
        public string LegalName { get; set; } = "Hotel Demo Raqmi SARL";
        public string Email { get; set; } = "contact@demo.raqmi.local";
        public string Phone { get; set; } = "+213 555 000 000";
        public string Address { get; set; } = "12 Rue Didouche Mourad, Alger";
        public string Currency { get; set; } = "DZD";
        public string DateFormat { get; set; } = "dd/MM/yyyy";
        public string NumberFormat { get; set; } = "fr-DZ";
        public string Timezone { get; set; } = "Africa/Algiers";
        public int PaymentDelayDays { get; set; } = 30;
        public int ReminderDelayDays { get; set; } = 7;
        public string BrandPrimaryColor { get; set; } = "#2563eb";
        public string? BrandLogoUrl { get; set; }
    }

    private static readonly State Settings = new();

    public static object Get() => ToDto(Settings);

    public static object Patch(SettingsPatchRequest body)
    {
        if (body.LegalName is not null) Settings.LegalName = body.LegalName.Trim();
        if (body.Email is not null) Settings.Email = body.Email.Trim();
        if (body.Phone is not null) Settings.Phone = body.Phone.Trim();
        if (body.Address is not null) Settings.Address = body.Address.Trim();
        if (body.Currency is not null) Settings.Currency = body.Currency.Trim().ToUpperInvariant();
        if (body.DateFormat is not null) Settings.DateFormat = body.DateFormat.Trim();
        if (body.NumberFormat is not null) Settings.NumberFormat = body.NumberFormat.Trim();
        if (body.Timezone is not null) Settings.Timezone = body.Timezone.Trim();
        if (body.PaymentDelayDays.HasValue) Settings.PaymentDelayDays = body.PaymentDelayDays.Value;
        if (body.ReminderDelayDays.HasValue) Settings.ReminderDelayDays = body.ReminderDelayDays.Value;
        if (body.BrandPrimaryColor is not null) Settings.BrandPrimaryColor = body.BrandPrimaryColor.Trim();
        if (body.BrandLogoUrl is not null) Settings.BrandLogoUrl = string.IsNullOrWhiteSpace(body.BrandLogoUrl) ? null : body.BrandLogoUrl.Trim();

        DemoAuditStore.Add("update", "settings", "TenantSettings", DemoData.Tenant.Id, "Paramètres entreprise mis à jour");
        return ToDto(Settings);
    }

    private static object ToDto(State s) => new
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

public sealed record SettingsPatchRequest(
    string? LegalName,
    string? Email,
    string? Phone,
    string? Address,
    string? Currency,
    string? DateFormat,
    string? NumberFormat,
    string? Timezone,
    int? PaymentDelayDays,
    int? ReminderDelayDays,
    string? BrandPrimaryColor,
    string? BrandLogoUrl);
