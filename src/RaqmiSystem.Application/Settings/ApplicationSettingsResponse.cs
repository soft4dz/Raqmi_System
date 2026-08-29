namespace RaqmiSystem.Application.Settings;

/// <summary>
/// The global settings as read by clients. <c>IsConfigured</c> is false while no row has been
/// written yet: the payload then carries the defaults the installation currently runs with, and
/// the establishment identity is still a placeholder.
/// </summary>
public sealed record ApplicationSettingsResponse(
    string CompanyName,
    string? CompanyNif,
    string? CompanyRc,
    string? CompanyAi,
    string? CompanyNis,
    string? CompanyAddress,
    string? CompanyCity,
    string? CompanyPhone,
    string? CompanyEmail,
    decimal DefaultVatRate,
    string CurrencyLabel,
    int AuditRetentionDays,
    bool IsConfigured,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
