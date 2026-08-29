namespace RaqmiSystem.Application.Settings;

public sealed record UpdateApplicationSettingsRequest(
    string CompanyName,
    decimal DefaultVatRate,
    int AuditRetentionDays,
    string? CompanyNif = null,
    string? CompanyRc = null,
    string? CompanyAi = null,
    string? CompanyNis = null,
    string? CompanyAddress = null,
    string? CompanyCity = null,
    string? CompanyPhone = null,
    string? CompanyEmail = null,
    string? CurrencyLabel = null);
