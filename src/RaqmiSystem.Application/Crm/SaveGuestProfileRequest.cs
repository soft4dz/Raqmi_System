namespace RaqmiSystem.Application.Crm;

/// <summary>
/// The editable half of a guest profile. Marketing consent is NOT part of it: it is recorded by
/// its own operation, because it is the answer the guest gave on a date and not a field a screen
/// happens to save along with a room preference.
/// </summary>
public sealed record SaveGuestProfileRequest(
    string? SegmentCode = null,
    string? PreferredLanguage = null,
    DateOnly? BirthDate = null,
    string? Preferences = null,
    string? Notes = null,
    bool IsVip = false);
