using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Kpi;

/// <summary>Un encaissement. Seul le statut Confirme est de l'argent reellement entre.</summary>
public sealed record KpiReceiptFact(
    string HotelUnitCode,
    DateOnly ReceiptDate,
    decimal Amount,
    ReceiptStatus Status);
