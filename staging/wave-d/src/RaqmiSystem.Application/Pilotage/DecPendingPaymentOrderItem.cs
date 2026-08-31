namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One treasury payment order still at the Draft status, i.e. awaiting approval. Age is
/// counted in days from the order date to the cockpit date.
/// </summary>
public sealed record DecPendingPaymentOrderItem(
    Guid Id,
    DateOnly OrderDate,
    string Beneficiary,
    decimal Amount,
    DateOnly DueDate,
    string BankAccountCode,
    int AgeDays);
