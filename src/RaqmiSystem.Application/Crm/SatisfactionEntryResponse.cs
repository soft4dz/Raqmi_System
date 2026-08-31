using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record SatisfactionEntryResponse(
    Guid Id,
    string CustomerCode,
    string CustomerName,
    string HotelUnitCode,
    DateOnly SurveyDate,
    int Score,
    NpsCategory Category,
    SatisfactionSource Source,
    Guid? ReservationId,
    string? Comment,
    DateTimeOffset CreatedAt,
    string CreatedBy);
