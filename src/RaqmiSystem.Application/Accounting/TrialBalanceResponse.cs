namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// Balance generale: one row per account touched over the period, plus the general totals.
///
/// READ <paramref name="PostedEntriesOnly"/> BEFORE READING THE NUMBERS. It is always true, and
/// it is surfaced as a field rather than left to the documentation because it is the rule that
/// surprises people: DRAFT ENTRIES ARE NOT COUNTED. A balance is a statement about the books,
/// and a draft has not entered the books - it may be unbalanced, half-typed, or abandoned
/// tomorrow. Cancelled drafts are excluded for the same reason. Reversing entries, on the other
/// hand, ARE counted (they are posted), which is exactly why a reversed entry nets to zero here
/// instead of vanishing.
///
/// <paramref name="Balance"/> - the general total debit minus the general total credit - is
/// expected to be exactly zero: every posted entry was balanced when it was posted, so their sum
/// is too. A non-zero value means the data was tampered with outside this module.
/// </summary>
public sealed record TrialBalanceResponse(
    DateOnly? From,
    DateOnly? To,
    bool PostedEntriesOnly,
    int AccountCount,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance,
    IReadOnlyCollection<TrialBalanceRow> Rows);
