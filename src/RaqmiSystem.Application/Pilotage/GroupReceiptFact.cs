using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One cash receipt of the period, with its status: the calculator applies the treasury
/// module's rule (only Confirmed receipts are money in) itself, in pure code.
/// </summary>
public sealed record GroupReceiptFact(
    string HotelUnitCode,
    decimal Amount,
    ReceiptStatus Status);
