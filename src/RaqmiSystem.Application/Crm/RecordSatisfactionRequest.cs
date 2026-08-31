using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record RecordSatisfactionRequest(
    string CustomerCode,
    string HotelUnitCode,
    DateOnly SurveyDate,
    int Score,
    SatisfactionSource Source,
    Guid? ReservationId = null,
    string? Comment = null);
