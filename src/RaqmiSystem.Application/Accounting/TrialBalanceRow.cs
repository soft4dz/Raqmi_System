using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// One account's movements over the requested period.
/// <paramref name="Balance"/> is <c>TotalDebit - TotalCredit</c>: positive means the account is
/// debtor over the period, negative means creditor. A single signed figure is used rather than
/// the two "solde debiteur / solde crediteur" columns of a printed balance, because those two
/// are just this number split by sign and a renderer can split it at display time.
///
/// <paramref name="AccountLabel"/> and <paramref name="AccountClass"/> are null when the code no
/// longer matches any account in the chart - which the module never causes on its own (a
/// referenced account cannot be deleted) but which a manual database edit could.
/// </summary>
public sealed record TrialBalanceRow(
    string AccountCode,
    string? AccountLabel,
    int? AccountClass,
    AccountKind? Kind,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);
