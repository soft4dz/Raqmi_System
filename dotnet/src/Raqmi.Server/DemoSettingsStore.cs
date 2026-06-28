namespace Raqmi.Server;

public static class DemoSettingsStore
{
    private sealed class State
    {
        public string LegalName { get; set; } = "Hotel Demo Raqmi SARL";
        public string TradeName { get; set; } = "Hôtel Demo Raqmi";
        public string LegalForm { get; set; } = "sarl";
        public string RegistrationNumber { get; set; } = "16/00-1234567B23";
        public string TaxId { get; set; } = "123456789012345";
        public string StatisticalId { get; set; } = "1234567890";
        public string VatArticle { get; set; } = "A123456789";
        public string ActivitySector { get; set; } = "Hôtellerie & restauration";
        public string Email { get; set; } = "contact@demo.raqmi.local";
        public string Phone { get; set; } = "+213 555 000 000";
        public string Website { get; set; } = "https://demo.raqmi.io";
        public string Address { get; set; } = "12 Rue Didouche Mourad";
        public string City { get; set; } = "Alger";
        public string Wilaya { get; set; } = "Alger";
        public string PostalCode { get; set; } = "16000";
        public string Country { get; set; } = "Algérie";
        public string Currency { get; set; } = "DZD";
        public string DateFormat { get; set; } = "dd/MM/yyyy";
        public string NumberFormat { get; set; } = "fr-DZ";
        public string Timezone { get; set; } = "Africa/Algiers";
        public int PaymentDelayDays { get; set; } = 30;
        public int ReminderDelayDays { get; set; } = 7;
        public string InvoicePrefix { get; set; } = "FAC";
        public string QuotePrefix { get; set; } = "DEV";
        public int NextInvoiceNumber { get; set; } = 42;
        public int FiscalYearStartMonth { get; set; } = 1;
        public decimal DefaultVatRate { get; set; } = 19;
        public string InvoiceFooter { get; set; } = "Merci de votre confiance. Paiement par virement bancaire.";
        public string AcceptedPaymentMethods { get; set; } = "virement,espèces,chèque,carte";
        public string BrandPrimaryColor { get; set; } = "#2563eb";
        public string BrandSecondaryColor { get; set; } = "#0f766e";
        public string? BrandLogoUrl { get; set; }
        public int SessionTimeoutMinutes { get; set; } = 480;
        public int PasswordMinLength { get; set; } = 8;
        public int ForcePasswordChangeDays { get; set; } = 90;
        public string StorageDriver { get; set; } = "local";
        public int MaxUploadSizeMb { get; set; } = 25;
        public string BackupFrequency { get; set; } = "daily";
        public int BackupRetentionDays { get; set; } = 30;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    private static readonly State Settings = new();

    public static object Get() => ToDto(Settings);

    public static object Patch(SettingsPatchRequest body)
    {
        if (body.LegalName is not null) Settings.LegalName = body.LegalName.Trim();
        if (body.TradeName is not null) Settings.TradeName = body.TradeName.Trim();
        if (body.LegalForm is not null) Settings.LegalForm = body.LegalForm.Trim();
        if (body.RegistrationNumber is not null) Settings.RegistrationNumber = body.RegistrationNumber.Trim();
        if (body.TaxId is not null) Settings.TaxId = body.TaxId.Trim();
        if (body.StatisticalId is not null) Settings.StatisticalId = body.StatisticalId.Trim();
        if (body.VatArticle is not null) Settings.VatArticle = body.VatArticle.Trim();
        if (body.ActivitySector is not null) Settings.ActivitySector = body.ActivitySector.Trim();
        if (body.Email is not null) Settings.Email = body.Email.Trim();
        if (body.Phone is not null) Settings.Phone = body.Phone.Trim();
        if (body.Website is not null) Settings.Website = body.Website.Trim();
        if (body.Address is not null) Settings.Address = body.Address.Trim();
        if (body.City is not null) Settings.City = body.City.Trim();
        if (body.Wilaya is not null) Settings.Wilaya = body.Wilaya.Trim();
        if (body.PostalCode is not null) Settings.PostalCode = body.PostalCode.Trim();
        if (body.Country is not null) Settings.Country = body.Country.Trim();
        if (body.Currency is not null) Settings.Currency = body.Currency.Trim().ToUpperInvariant();
        if (body.DateFormat is not null) Settings.DateFormat = body.DateFormat.Trim();
        if (body.NumberFormat is not null) Settings.NumberFormat = body.NumberFormat.Trim();
        if (body.Timezone is not null) Settings.Timezone = body.Timezone.Trim();
        if (body.PaymentDelayDays.HasValue) Settings.PaymentDelayDays = body.PaymentDelayDays.Value;
        if (body.ReminderDelayDays.HasValue) Settings.ReminderDelayDays = body.ReminderDelayDays.Value;
        if (body.InvoicePrefix is not null) Settings.InvoicePrefix = body.InvoicePrefix.Trim();
        if (body.QuotePrefix is not null) Settings.QuotePrefix = body.QuotePrefix.Trim();
        if (body.NextInvoiceNumber.HasValue) Settings.NextInvoiceNumber = body.NextInvoiceNumber.Value;
        if (body.FiscalYearStartMonth.HasValue) Settings.FiscalYearStartMonth = body.FiscalYearStartMonth.Value;
        if (body.DefaultVatRate.HasValue) Settings.DefaultVatRate = body.DefaultVatRate.Value;
        if (body.InvoiceFooter is not null) Settings.InvoiceFooter = body.InvoiceFooter.Trim();
        if (body.AcceptedPaymentMethods is not null) Settings.AcceptedPaymentMethods = body.AcceptedPaymentMethods.Trim();
        if (body.BrandPrimaryColor is not null) Settings.BrandPrimaryColor = body.BrandPrimaryColor.Trim();
        if (body.BrandSecondaryColor is not null) Settings.BrandSecondaryColor = body.BrandSecondaryColor.Trim();
        if (body.BrandLogoUrl is not null) Settings.BrandLogoUrl = string.IsNullOrWhiteSpace(body.BrandLogoUrl) ? null : body.BrandLogoUrl.Trim();
        if (body.SessionTimeoutMinutes.HasValue) Settings.SessionTimeoutMinutes = body.SessionTimeoutMinutes.Value;
        if (body.PasswordMinLength.HasValue) Settings.PasswordMinLength = body.PasswordMinLength.Value;
        if (body.ForcePasswordChangeDays.HasValue) Settings.ForcePasswordChangeDays = body.ForcePasswordChangeDays.Value;
        if (body.StorageDriver is not null) Settings.StorageDriver = body.StorageDriver.Trim();
        if (body.MaxUploadSizeMb.HasValue) Settings.MaxUploadSizeMb = body.MaxUploadSizeMb.Value;
        if (body.BackupFrequency is not null) Settings.BackupFrequency = body.BackupFrequency.Trim();
        if (body.BackupRetentionDays.HasValue) Settings.BackupRetentionDays = body.BackupRetentionDays.Value;

        Settings.UpdatedAt = DateTime.UtcNow;
        DemoAuditStore.Add("update", "settings", "TenantSettings", DemoData.Tenant.Id, "Paramètres entreprise mis à jour");
        return ToDto(Settings);
    }

    private static object ToDto(State s) => new
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

public sealed record SettingsPatchRequest(
    string? LegalName,
    string? TradeName,
    string? LegalForm,
    string? RegistrationNumber,
    string? TaxId,
    string? StatisticalId,
    string? VatArticle,
    string? ActivitySector,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? City,
    string? Wilaya,
    string? PostalCode,
    string? Country,
    string? Currency,
    string? DateFormat,
    string? NumberFormat,
    string? Timezone,
    int? PaymentDelayDays,
    int? ReminderDelayDays,
    string? InvoicePrefix,
    string? QuotePrefix,
    int? NextInvoiceNumber,
    int? FiscalYearStartMonth,
    decimal? DefaultVatRate,
    string? InvoiceFooter,
    string? AcceptedPaymentMethods,
    string? BrandPrimaryColor,
    string? BrandSecondaryColor,
    string? BrandLogoUrl,
    int? SessionTimeoutMinutes,
    int? PasswordMinLength,
    int? ForcePasswordChangeDays,
    string? StorageDriver,
    int? MaxUploadSizeMb,
    string? BackupFrequency,
    int? BackupRetentionDays);
