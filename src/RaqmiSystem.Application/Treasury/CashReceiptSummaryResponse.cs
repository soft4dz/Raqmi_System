using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Treasury;

public sealed record CashReceiptSummaryResponse(
    DateOnly? From,
    DateOnly? To,
    string? HotelUnitCode,
    ReceiptStatus? Status,
    int TotalCount,
    int DraftCount,
    int ConfirmedCount,
    int CancelledCount,
    decimal CashTotal,
    decimal CardTotal,
    decimal ChequeTotal,
    decimal BankTransferTotal,
    decimal GrandTotal);
