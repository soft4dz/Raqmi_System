using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Accounting;

/// <summary>
/// SCF accounting service. Every accounting invariant (one-side-only lines, balance, double
/// entry, immutability of a posted entry, correction by reversal) lives in the domain entities;
/// this class only orchestrates persistence, referential checks and auditing around them.
/// </summary>
public sealed class AccountingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IAccountingService
{
    private const string ChartAccountsEntity = "accounting.chart_accounts";

    private const string JournalsEntity = "accounting.journals";

    private const string JournalEntriesEntity = "accounting.journal_entries";

    /// <summary>
    /// Answer given when the atomic draft claim (see <see cref="TryClaimDraftEntryAsync"/>) finds
    /// that the entry loaded as a draft is no longer one: a concurrent request posted or cancelled
    /// it between our read and our write. Nothing was modified.
    /// </summary>
    private const string ConcurrentEntryMutationRefused =
        "This journal entry was just posted or cancelled by a concurrent operation, so this change " +
        "was rolled back and nothing was modified. Reload the entry and try again.";

    public async Task<IReadOnlyCollection<ChartAccountResponse>> ListAccountsAsync(
        string? search,
        int? accountClass,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ChartAccount>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(account => account.IsActive);
        }

        if (accountClass.HasValue)
        {
            query = query.Where(account => account.AccountClass == accountClass.Value);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(account =>
                account.Code.Contains(normalizedSearch) ||
                account.Label.ToUpper().Contains(normalizedSearch));
        }

        var accounts = await query
            .OrderBy(account => account.Code)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<ChartAccountResponse>> GetAccountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeAccountCodeOrEmpty(code);

        var account = await dbContext.Set<ChartAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<ChartAccountResponse>.NotFound("Chart account was not found.");
        }

        return ApplicationResult<ChartAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<ChartAccountResponse>> CreateAccountAsync(
        CreateChartAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ChartAccount account;

        try
        {
            account = new ChartAccount(request.Code, request.Label, request.Kind);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ChartAccountResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<ChartAccount>()
            .AnyAsync(current => current.Code == account.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<ChartAccountResponse>.Conflict("A chart account with this code already exists.");
        }

        account.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<ChartAccount>().Add(account);

        try
        {
            await WriteAuditAsync(
                "accounting.chart_account.created",
                ChartAccountsEntity,
                account.Id,
                context,
                new { account.Code, account.Label, account.AccountClass, Kind = account.Kind.ToString() },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with the
            // same code loses the race against ux_chart_accounts_code.
            return ApplicationResult<ChartAccountResponse>.Conflict("A chart account with this code already exists.");
        }

        return ApplicationResult<ChartAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<ChartAccountResponse>> UpdateAccountAsync(
        string code,
        UpdateChartAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeAccountCodeOrEmpty(code);

        var account = await dbContext.Set<ChartAccount>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<ChartAccountResponse>.NotFound("Chart account was not found.");
        }

        try
        {
            account.UpdateDetails(request.Label, request.Kind);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ChartAccountResponse>.Validation(ex.Message);
        }

        account.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "accounting.chart_account.updated",
            ChartAccountsEntity,
            account.Id,
            context,
            new { account.Code, account.Label, Kind = account.Kind.ToString() },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ChartAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<ChartAccountResponse>> SetAccountActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeAccountCodeOrEmpty(code);

        var account = await dbContext.Set<ChartAccount>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<ChartAccountResponse>.NotFound("Chart account was not found.");
        }

        if (isActive)
        {
            account.Activate();
        }
        else
        {
            account.Deactivate();
        }

        account.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "accounting.chart_account.activated" : "accounting.chart_account.deactivated",
            ChartAccountsEntity,
            account.Id,
            context,
            new { account.Code, account.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ChartAccountResponse>.Success(Map(account));
    }

    public async Task<IReadOnlyCollection<AccountingJournalResponse>> ListJournalsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<AccountingJournal>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(journal => journal.IsActive);
        }

        var journals = await query
            .OrderBy(journal => journal.Code)
            .ToArrayAsync(cancellationToken);

        return journals.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<AccountingJournalResponse>> GetJournalAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeJournalCodeOrEmpty(code);

        var journal = await dbContext.Set<AccountingJournal>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (journal is null)
        {
            return ApplicationResult<AccountingJournalResponse>.NotFound("Journal was not found.");
        }

        return ApplicationResult<AccountingJournalResponse>.Success(Map(journal));
    }

    public async Task<ApplicationResult<AccountingJournalResponse>> CreateJournalAsync(
        CreateAccountingJournalRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        AccountingJournal journal;

        try
        {
            journal = new AccountingJournal(request.Code, request.Label);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<AccountingJournalResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<AccountingJournal>()
            .AnyAsync(current => current.Code == journal.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<AccountingJournalResponse>.Conflict("A journal with this code already exists.");
        }

        journal.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<AccountingJournal>().Add(journal);

        try
        {
            await WriteAuditAsync(
                "accounting.journal.created",
                JournalsEntity,
                journal.Id,
                context,
                new { journal.Code, journal.Label },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<AccountingJournalResponse>.Conflict("A journal with this code already exists.");
        }

        return ApplicationResult<AccountingJournalResponse>.Success(Map(journal));
    }

    public async Task<ApplicationResult<AccountingJournalResponse>> UpdateJournalAsync(
        string code,
        UpdateAccountingJournalRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeJournalCodeOrEmpty(code);

        var journal = await dbContext.Set<AccountingJournal>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (journal is null)
        {
            return ApplicationResult<AccountingJournalResponse>.NotFound("Journal was not found.");
        }

        try
        {
            journal.UpdateDetails(request.Label);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<AccountingJournalResponse>.Validation(ex.Message);
        }

        journal.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "accounting.journal.updated",
            JournalsEntity,
            journal.Id,
            context,
            new { journal.Code, journal.Label },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<AccountingJournalResponse>.Success(Map(journal));
    }

    public async Task<ApplicationResult<AccountingJournalResponse>> SetJournalActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeJournalCodeOrEmpty(code);

        var journal = await dbContext.Set<AccountingJournal>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (journal is null)
        {
            return ApplicationResult<AccountingJournalResponse>.NotFound("Journal was not found.");
        }

        if (isActive)
        {
            journal.Activate();
        }
        else
        {
            journal.Deactivate();
        }

        journal.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "accounting.journal.activated" : "accounting.journal.deactivated",
            JournalsEntity,
            journal.Id,
            context,
            new { journal.Code, journal.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<AccountingJournalResponse>.Success(Map(journal));
    }

    public async Task<IReadOnlyCollection<JournalEntryResponse>> ListEntriesAsync(
        DateOnly? from,
        DateOnly? to,
        string? journalCode,
        EntryStatus? status,
        string? accountCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(entry => entry.EntryDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(entry => entry.EntryDate <= to.Value);
        }

        var normalizedJournalCode = NormalizeNullableJournalCode(journalCode);

        if (normalizedJournalCode is not null)
        {
            query = query.Where(entry => entry.JournalCode == normalizedJournalCode);
        }

        if (status.HasValue)
        {
            query = query.Where(entry => entry.Status == status.Value);
        }

        var normalizedAccountCode = NormalizeNullableAccountCode(accountCode);

        if (normalizedAccountCode is not null)
        {
            query = query.Where(entry => entry.Lines.Any(line => line.AccountCode == normalizedAccountCode));
        }

        var entries = await query
            .OrderByDescending(entry => entry.EntryDate)
            .ThenBy(entry => entry.JournalCode)
            .ThenBy(entry => entry.Label)
            .ToArrayAsync(cancellationToken);

        return entries.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<JournalEntryResponse>> GetEntryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (entry is null)
        {
            return ApplicationResult<JournalEntryResponse>.NotFound("Journal entry was not found.");
        }

        return ApplicationResult<JournalEntryResponse>.Success(Map(entry));
    }

    public async Task<ApplicationResult<JournalEntryResponse>> CreateEntryAsync(
        CreateJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<JournalEntryResponse>.Validation("A journal entry must contain at least one line.");
        }

        JournalEntry entry;
        List<JournalEntryLine> lines;

        try
        {
            entry = new JournalEntry(request.EntryDate, request.JournalCode, request.Label, request.Reference);
            lines = BuildLines(request.Lines);
            entry.ReplaceLines(lines);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<JournalEntryResponse>.Validation(ex.Message);
        }

        var periodFailure = await RequireOpenPeriodAsync(entry.EntryDate, cancellationToken);
        if (periodFailure is not null) return periodFailure;

        var referenceFailure = await ValidateReferencesAsync(
            entry.JournalCode,
            DistinctAccountCodes(entry),
            cancellationToken);

        if (referenceFailure is not null)
        {
            return referenceFailure;
        }

        entry.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<JournalEntry>().Add(entry);

        await WriteAuditAsync(
            "accounting.journal_entry.created",
            JournalEntriesEntity,
            entry.Id,
            context,
            new { entry.JournalCode, entry.EntryDate, entry.TotalDebit, entry.TotalCredit, LineCount = entry.Lines.Count },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<JournalEntryResponse>.Success(Map(entry));
    }

    public async Task<ApplicationResult<JournalEntryResponse>> UpdateEntryLinesAsync(
        Guid id,
        UpdateJournalEntryLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<JournalEntryResponse>.Validation("A journal entry must contain at least one line.");
        }

        // Read, check and write happen inside one Serializable transaction, and the draft status
        // is re-asserted by the atomic claim below: without both, checking "is it a draft?" in
        // memory and persisting afterwards leaves the classic TOCTOU window in which a concurrent
        // /post slips between the check and the save, and a POSTED - therefore immutable - entry
        // gets its lines rewritten. Same pattern as UserAdministrationService.RunGuardedMutationAsync.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var entry = await dbContext.Set<JournalEntry>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (entry is null)
            {
                return ApplicationResult<JournalEntryResponse>.NotFound("Journal entry was not found.");
            }

            // The entity refuses this too, but the status is checked here first so that the
            // immutability of a posted entry surfaces as a 409 Conflict (the state of the resource
            // forbids the operation) rather than as a 400 among the input-validation failures.
            var editableFailure = RequireEditable(entry);

            if (editableFailure is not null)
            {
                return editableFailure;
            }

            var now = DateTimeOffset.UtcNow;

            // The status just checked in memory is re-asserted as the WHERE clause of a single
            // conditional UPDATE: only the request whose statement actually matched the row goes
            // on to mutate. A concurrent posting or cancellation makes the claim miss, and the
            // refusal is a retryable 409 rather than a silent corruption.
            if (!await TryClaimDraftEntryAsync(entry.Id, now, cancellationToken))
            {
                return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
            }

            try
            {
                entry.ReplaceLines(BuildLines(request.Lines));
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<JournalEntryResponse>.Validation(ex.Message);
            }

            var referenceFailure = await ValidateReferencesAsync(
                entry.JournalCode,
                DistinctAccountCodes(entry),
                cancellationToken);

            if (referenceFailure is not null)
            {
                return referenceFailure;
            }

            entry.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "accounting.journal_entry.lines_updated",
                JournalEntriesEntity,
                entry.Id,
                context,
                new { entry.JournalCode, entry.TotalDebit, entry.TotalCredit, LineCount = entry.Lines.Count },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<JournalEntryResponse>.Success(Map(entry));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
        }
    }

    public async Task<ApplicationResult<JournalEntryResponse>> PostEntryAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Same Serializable transaction + atomic draft claim as UpdateEntryLinesAsync: posting
        // reads the lines to validate the balance, so a concurrent line rewrite between that read
        // and this commit would let an entry enter the books with lines nobody validated.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var entry = await dbContext.Set<JournalEntry>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (entry is null)
            {
                return ApplicationResult<JournalEntryResponse>.NotFound("Journal entry was not found.");
            }

            // Checked before the reference re-check below so that a double-click on /post answers
            // "already posted" (409) rather than whatever the references happen to say now.
            if (entry.Status != EntryStatus.Draft)
            {
                return ApplicationResult<JournalEntryResponse>.Conflict(
                    entry.Status == EntryStatus.Posted
                        ? "This journal entry has already been posted."
                        : "A cancelled journal entry cannot be posted.");
            }

            var periodFailure = await RequireOpenPeriodAsync(entry.EntryDate, cancellationToken);
            if (periodFailure is not null) return periodFailure;

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimDraftEntryAsync(entry.Id, now, cancellationToken))
            {
                return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
            }

            // Posting engages the accounts, so the references are re-checked here and not only at
            // capture time: an account or a journal may have been deactivated while the entry sat in
            // the drafts.
            var referenceFailure = await ValidateReferencesAsync(
                entry.JournalCode,
                DistinctAccountCodes(entry),
                cancellationToken);

            if (referenceFailure is not null)
            {
                return referenceFailure;
            }

            try
            {
                await AssignDefinitiveNumberAsync(entry, cancellationToken);
                entry.Post(context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<JournalEntryResponse>.Validation(ex.Message);
            }

            entry.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "accounting.journal_entry.posted",
                JournalEntriesEntity,
                entry.Id,
                context,
                new { entry.JournalCode, entry.EntryDate, entry.TotalDebit, entry.TotalCredit },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<JournalEntryResponse>.Success(Map(entry));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
        }
    }

    public async Task<ApplicationResult<JournalEntryResponse>> ReverseEntryAsync(
        Guid id,
        ReverseJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.Set<JournalEntry>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (entry is null)
        {
            return ApplicationResult<JournalEntryResponse>.NotFound("Journal entry was not found.");
        }

        if (entry.IsReversed)
        {
            return ApplicationResult<JournalEntryResponse>.Conflict("This journal entry has already been reversed.");
        }

        // Deliberately NO active-reference check here. A reversal must always remain possible:
        // refusing to correct an entry because one of the accounts it already touched has since
        // been deactivated would leave a wrong entry in the books with no legal way out.
        var now = DateTimeOffset.UtcNow;
        var reversalDate = request.ReversalDate ?? entry.EntryDate;
        var periodFailure = await RequireOpenPeriodAsync(reversalDate, cancellationToken);
        if (periodFailure is not null) return periodFailure;
        JournalEntry reversal;

        try
        {
            reversal = entry.CreateReversal(request.ReversalDate, request.Reference, context.UserName, now);
            await AssignDefinitiveNumberAsync(reversal, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<JournalEntryResponse>.Validation(ex.Message);
        }

        reversal.MarkCreated(context.UserName, now);
        entry.MarkUpdated(context.UserName, now);
        dbContext.Set<JournalEntry>().Add(reversal);

        try
        {
            await WriteAuditAsync(
                "accounting.journal_entry.reversed",
                JournalEntriesEntity,
                entry.Id,
                context,
                new
                {
                    entry.JournalCode,
                    ReversedEntryId = entry.Id,
                    ReversalEntryId = reversal.Id,
                    reversal.EntryDate,
                    reversal.TotalDebit,
                    reversal.TotalCredit
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // ux_journal_entries_reverses_entry_id: a concurrent request already recorded the
            // reversal of this very entry. Answering 409 rather than letting a second, duplicate
            // contrepassation into the books is the whole point of that index.
            return ApplicationResult<JournalEntryResponse>.Conflict(
                "This journal entry has already been reversed by a concurrent operation.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationResult<JournalEntryResponse>.Conflict(
                "The journal sequence changed concurrently. Reload and retry the reversal.");
        }

        // The NEW entry is returned: it is the document the caller has to file, and the reversed
        // one is reachable through its ReversesEntryId.
        return ApplicationResult<JournalEntryResponse>.Success(Map(reversal));
    }

    private async Task<ApplicationResult<JournalEntryResponse>?> RequireOpenPeriodAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var period = await dbContext.AccountingPeriods.AsNoTracking()
            .SingleOrDefaultAsync(x => date >= x.StartsOn && date <= x.EndsOn, cancellationToken);
        if (period is null)
        {
            // Backward-compatible bootstrap: period enforcement becomes strict as soon as the
            // establishment configures its first fiscal year.
            if (!await dbContext.AccountingPeriods.AnyAsync(cancellationToken)) return null;
            return ApplicationResult<JournalEntryResponse>.Validation("The entry date is outside every configured accounting period.");
        }
        if (period.Status == AccountingPeriodStatus.Closed)
            return ApplicationResult<JournalEntryResponse>.Conflict("The accounting period is closed; capture and posting are forbidden.");
        return null;
    }

    public async Task<ApplicationResult<JournalEntryResponse>> CancelEntryAsync(
        Guid id,
        CancelJournalEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Same Serializable transaction + atomic draft claim as UpdateEntryLinesAsync: a
        // cancellation racing a posting must lose against the posted - immutable - entry rather
        // than quietly stamp it Cancelled.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var entry = await dbContext.Set<JournalEntry>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (entry is null)
            {
                return ApplicationResult<JournalEntryResponse>.NotFound("Journal entry was not found.");
            }

            if (entry.Status == EntryStatus.Posted)
            {
                return ApplicationResult<JournalEntryResponse>.Conflict(
                    "A posted journal entry cannot be cancelled. Record a reversing entry instead " +
                    "(POST /accounting/entries/{id}/reverse).");
            }

            var now = DateTimeOffset.UtcNow;

            // Claimed only when the loaded status is Draft: an entry already seen as Cancelled
            // falls through to entry.Cancel, whose own refusal names the real reason instead of a
            // concurrency that never happened.
            if (entry.Status == EntryStatus.Draft
                && !await TryClaimDraftEntryAsync(entry.Id, now, cancellationToken))
            {
                return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
            }

            try
            {
                entry.Cancel(request.Reason, context.UserName, now);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<JournalEntryResponse>.Validation(ex.Message);
            }

            entry.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "accounting.journal_entry.cancelled",
                JournalEntriesEntity,
                entry.Id,
                context,
                new { entry.JournalCode, entry.EntryDate, entry.CancellationReason },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<JournalEntryResponse>.Success(Map(entry));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<JournalEntryResponse>.Conflict(ConcurrentEntryMutationRefused);
        }
    }

    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        // POSTED ONLY. Drafts (unbalanced, half-typed, possibly abandoned) and cancelled drafts
        // have not entered the books and must not appear in a balance; reversing entries are
        // posted and therefore DO appear, which is what makes a reversed entry net to zero here
        // rather than disappear. The flag is echoed in the response so no reader has to guess.
        var entries = dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.Status == EntryStatus.Posted);

        if (from.HasValue)
        {
            entries = entries.Where(entry => entry.EntryDate >= from.Value);
        }

        if (to.HasValue)
        {
            entries = entries.Where(entry => entry.EntryDate <= to.Value);
        }

        // The movements are filtered in the database and summed in memory: the SQLite provider
        // used by the integration-test harness stores decimal as TEXT, so a server-side SUM()
        // over a decimal column would not add up the way PostgreSQL's numeric does. Same reason
        // as TreasuryService.GetReceiptSummaryAsync and DailyRevenueService.GetSummaryAsync.
        var movements = await (
                from line in dbContext.Set<JournalEntryLine>().AsNoTracking()
                join entry in entries on line.JournalEntryId equals entry.Id
                select new { line.AccountCode, line.Debit, line.Credit })
            .ToArrayAsync(cancellationToken);

        var accountCodes = movements.Select(movement => movement.AccountCode).Distinct().ToArray();

        var accounts = accountCodes.Length == 0
            ? new Dictionary<string, ChartAccount>()
            : await dbContext.Set<ChartAccount>()
                .AsNoTracking()
                .Where(account => accountCodes.Contains(account.Code))
                .ToDictionaryAsync(account => account.Code, cancellationToken);

        var rows = movements
            .GroupBy(movement => movement.AccountCode)
            .Select(group =>
            {
                var totalDebit = group.Sum(movement => movement.Debit);
                var totalCredit = group.Sum(movement => movement.Credit);
                var account = accounts.GetValueOrDefault(group.Key);

                return new TrialBalanceRow(
                    group.Key,
                    account?.Label,
                    account?.AccountClass,
                    account?.Kind,
                    totalDebit,
                    totalCredit,
                    totalDebit - totalCredit);
            })
            .OrderBy(row => row.AccountCode, StringComparer.Ordinal)
            .ToArray();

        var generalDebit = rows.Sum(row => row.TotalDebit);
        var generalCredit = rows.Sum(row => row.TotalCredit);

        return new TrialBalanceResponse(
            from,
            to,
            PostedEntriesOnly: true,
            rows.Length,
            generalDebit,
            generalCredit,
            generalDebit - generalCredit,
            rows);
    }

    /// <summary>
    /// Atomic form of "this entry is still a draft". The invariant travels as the WHERE clause of
    /// one conditional UPDATE on the entry's own row, so it is evaluated by the database at the
    /// instant the row is claimed rather than answered by the earlier SELECT that a concurrent
    /// posting or cancellation can invalidate - the claim-in-one-statement pattern of
    /// <c>UserAdministrationService.TryClaimAnotherActiveAdministratorAsync</c>. Returns true only
    /// when the statement really matched the row.
    ///
    /// The single column it writes, <c>UpdatedAt</c>, is one the caller's mutation is about to
    /// stamp anyway with the very same timestamp: the claim adds no state of its own, it only
    /// needs to be a write so that the row is claimed, not merely read.
    /// </summary>
    private async Task<bool> TryClaimDraftEntryAsync(
        Guid entryId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<JournalEntry>()
            .Where(current => current.Id == entryId && current.Status == EntryStatus.Draft)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Refuses an edit of anything but a draft, and distinguishes the two refusals: a POSTED
    /// entry is a conflict with the resource's state (409) and points at the reversal route,
    /// while a cancelled draft is simply gone (400).
    /// </summary>
    private static ApplicationResult<JournalEntryResponse>? RequireEditable(JournalEntry entry)
    {
        if (entry.Status == EntryStatus.Posted)
        {
            return ApplicationResult<JournalEntryResponse>.Conflict(
                "A posted journal entry is immutable. Correct it with a reversing entry instead " +
                "(POST /accounting/entries/{id}/reverse).");
        }

        if (entry.Status != EntryStatus.Draft)
        {
            return ApplicationResult<JournalEntryResponse>.Validation(
                "A cancelled journal entry can no longer be modified.");
        }

        return null;
    }

    /// <summary>
    /// Checks that the journal and every referenced account exist and are active. Returns null
    /// when everything checks out, otherwise the failure to hand straight back to the caller.
    /// </summary>
    private async Task<ApplicationResult<JournalEntryResponse>?> ValidateReferencesAsync(
        string journalCode,
        IReadOnlyCollection<string> accountCodes,
        CancellationToken cancellationToken)
    {
        var journal = await dbContext.Set<AccountingJournal>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == journalCode, cancellationToken);

        if (journal is null)
        {
            return ApplicationResult<JournalEntryResponse>.NotFound($"Journal '{journalCode}' was not found.");
        }

        if (!journal.IsActive)
        {
            return ApplicationResult<JournalEntryResponse>.Validation(
                $"Journal '{journalCode}' is inactive and cannot receive entries.");
        }

        var codes = accountCodes.ToArray();

        var accounts = await dbContext.Set<ChartAccount>()
            .AsNoTracking()
            .Where(account => codes.Contains(account.Code))
            .ToArrayAsync(cancellationToken);

        var missing = codes.Except(accounts.Select(account => account.Code)).ToArray();

        if (missing.Length > 0)
        {
            return ApplicationResult<JournalEntryResponse>.NotFound(
                $"Unknown chart accounts: {string.Join(", ", missing)}.");
        }

        var inactive = accounts
            .Where(account => !account.IsActive)
            .Select(account => account.Code)
            .ToArray();

        if (inactive.Length > 0)
        {
            return ApplicationResult<JournalEntryResponse>.Validation(
                $"Inactive chart accounts cannot be used: {string.Join(", ", inactive)}.");
        }

        return null;
    }

    private static IReadOnlyCollection<string> DistinctAccountCodes(JournalEntry entry)
    {
        return entry.Lines
            .Select(line => line.AccountCode)
            .Distinct()
            .ToArray();
    }

    private static List<JournalEntryLine> BuildLines(IReadOnlyCollection<JournalEntryLineRequest> requests)
    {
        return requests
            .Select(line => new JournalEntryLine(line.AccountCode, line.Label, line.Debit, line.Credit))
            .ToList();
    }

    private static ChartAccountResponse Map(ChartAccount account)
    {
        return new ChartAccountResponse(
            account.Id,
            account.Code,
            account.Label,
            account.AccountClass,
            AccountClassCatalog.LabelOf(account.AccountClass),
            account.Kind,
            account.IsActive,
            account.CreatedAt,
            account.CreatedBy,
            account.UpdatedAt,
            account.UpdatedBy);
    }

    private static AccountingJournalResponse Map(AccountingJournal journal)
    {
        return new AccountingJournalResponse(
            journal.Id,
            journal.Code,
            journal.Label,
            journal.IsActive,
            journal.CreatedAt,
            journal.CreatedBy,
            journal.UpdatedAt,
            journal.UpdatedBy);
    }

    private static JournalEntryResponse Map(JournalEntry entry)
    {
        var lines = entry.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new JournalEntryLineResponse(
                line.Id,
                line.LineNumber,
                line.AccountCode,
                line.Label,
                line.Debit,
                line.Credit))
            .ToArray();

        return new JournalEntryResponse(
            entry.Id,
            entry.EntryDate,
            entry.JournalCode,
            entry.Label,
            entry.Reference,
            entry.Status,
            entry.TotalDebit,
            entry.TotalCredit,
            entry.IsBalanced,
            entry.CanEdit,
            lines,
            entry.ReversesEntryId,
            entry.ReversedByEntryId,
            entry.PostedAt,
            entry.PostedBy,
            entry.ReversedAt,
            entry.ReversedBy,
            entry.CancelledAt,
            entry.CancelledBy,
            entry.CancellationReason,
            entry.CreatedAt,
            entry.CreatedBy,
            entry.UpdatedAt,
            entry.UpdatedBy,
            entry.DocumentNumber,
            entry.FiscalYearId);
    }

    /// <summary>
    /// Lookup normalization for a code coming from a route or a query string. Deliberately does
    /// not go through <see cref="ChartAccount.NormalizeCode"/>: a malformed code must produce a
    /// clean 404 (nothing matches) rather than an exception, while a code being CREATED does go
    /// through the strict normalization, in the entity's constructor.
    /// </summary>
    private static string NormalizeAccountCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
    }

    private static string? NormalizeNullableAccountCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    }

    private static string NormalizeJournalCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableJournalCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the audit
    /// row), so this call is usually a no-op - it exists so persistence never silently depends on
    /// the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AssignDefinitiveNumberAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        var period = await dbContext.AccountingPeriods.SingleOrDefaultAsync(
            x => entry.EntryDate >= x.StartsOn && entry.EntryDate <= x.EndsOn, cancellationToken);
        if (period is null) return;
        var year = await dbContext.FiscalYears.SingleAsync(x => x.Id == period.FiscalYearId, cancellationToken);
        var sequence = await dbContext.JournalSequences.SingleOrDefaultAsync(
            x => x.JournalCode == entry.JournalCode && x.FiscalYearId == year.Id, cancellationToken);
        if (sequence is null) { sequence = new JournalSequence(entry.JournalCode, year.Id); dbContext.JournalSequences.Add(sequence); }
        var number = sequence.Next();
        entry.AssignDocumentNumber(year.Id, $"{entry.JournalCode}-{year.Code}-{number:000000}");
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
