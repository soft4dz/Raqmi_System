using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// SCF accounting: chart of accounts, journals, entries and trial balance.
///
/// Note what this interface does NOT offer: no delete, on any of the three. Accounts and
/// journals are deactivated (<c>SetAccountActiveAsync</c> / <c>SetJournalActiveAsync</c>) because
/// posted lines reference them for good, and a posted entry is corrected only by
/// <see cref="ReverseEntryAsync"/>. <see cref="CancelEntryAsync"/> applies to drafts alone.
/// </summary>
public interface IAccountingService
{
    Task<IReadOnlyCollection<ChartAccountResponse>> ListAccountsAsync(
        string? search,
        int? accountClass,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ChartAccountResponse>> GetAccountAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ChartAccountResponse>> CreateAccountAsync(
        CreateChartAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ChartAccountResponse>> UpdateAccountAsync(
        string code,
        UpdateChartAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ChartAccountResponse>> SetAccountActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccountingJournalResponse>> ListJournalsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AccountingJournalResponse>> GetJournalAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AccountingJournalResponse>> CreateJournalAsync(
        CreateAccountingJournalRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AccountingJournalResponse>> UpdateJournalAsync(
        string code,
        UpdateAccountingJournalRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AccountingJournalResponse>> SetJournalActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<JournalEntryResponse>> ListEntriesAsync(
        DateOnly? from,
        DateOnly? to,
        string? journalCode,
        EntryStatus? status,
        string? accountCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<JournalEntryResponse>> GetEntryAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>Creates a DRAFT entry, which is allowed to be unbalanced.</summary>
    Task<ApplicationResult<JournalEntryResponse>> CreateEntryAsync(
        CreateJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Replaces the lines of a draft. Conflict (409) on a posted entry.</summary>
    Task<ApplicationResult<JournalEntryResponse>> UpdateEntryLinesAsync(
        Guid id,
        UpdateJournalEntryLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enters the draft in the books. Refused (400) while it is unbalanced or carries fewer than
    /// two lines - the entry then simply stays a draft.
    /// </summary>
    Task<ApplicationResult<JournalEntryResponse>> PostEntryAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the reversing entry (extourne) of a posted entry and returns THE NEW ENTRY, not
    /// the corrected one. The corrected entry stays posted, flagged with
    /// <c>ReversedByEntryId</c>.
    /// </summary>
    Task<ApplicationResult<JournalEntryResponse>> ReverseEntryAsync(
        Guid id,
        ReverseJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Abandons a draft. Refused on a posted entry.</summary>
    Task<ApplicationResult<JournalEntryResponse>> CancelEntryAsync(
        Guid id,
        CancelJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Trial balance over the period, POSTED ENTRIES ONLY (the returned
    /// <see cref="TrialBalanceResponse.PostedEntriesOnly"/> flag says so in the payload itself).
    /// Both bounds are inclusive and both are optional; an open bound means "since the
    /// beginning" / "until the end".
    /// </summary>
    Task<TrialBalanceResponse> GetTrialBalanceAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);
}
