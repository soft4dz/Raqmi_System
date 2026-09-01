using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// An accounting entry (ecriture comptable): a dated set of lines recorded in one journal.
///
/// The four invariants below are the whole point of the module, and they live here rather than
/// in the service so no caller - API, importer, future desktop client - can go around them:
///
/// 1. DOUBLE ENTRY. Posting requires at least two lines: a single line cannot express the
///    counterpart of anything.
/// 2. BALANCE. Posting requires total debit = total credit. An unbalanced entry is not refused
///    outright, it simply stays a Draft: work in progress is normal, entering it in the books is
///    what is gated.
/// 3. IMMUTABILITY. A Posted entry accepts no change - not its lines, not its header, not its
///    date. <see cref="ReplaceLines"/> and <see cref="UpdateHeader"/> throw.
/// 4. CORRECTION BY REVERSAL. The only way to undo a posted entry is
///    <see cref="CreateReversal"/>, which records a NEW entry mirroring every line and flags the
///    original as reversed. The original stays Posted and stays in the books: a correction that
///    erased its own trace would defeat the point. There is deliberately no delete operation on
///    a posted entry anywhere in this module.
/// </summary>
public sealed class JournalEntry : AuditableEntity
{
    public const int MaxLabelLength = 300;

    public const int MaxReferenceLength = 80;

    private readonly List<JournalEntryLine> _lines = new();

    private JournalEntry()
    {
    }

    public JournalEntry(DateOnly entryDate, string journalCode, string label, string? reference = null)
    {
        EntryDate = entryDate;
        JournalCode = AccountingJournal.NormalizeCode(journalCode);
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        Reference = NormalizeOptional(reference, nameof(reference), MaxReferenceLength);
        Status = EntryStatus.Draft;
    }

    public DateOnly EntryDate { get; private set; }

    public string JournalCode { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>External reference (invoice number, receipt number, voucher...), optional.</summary>
    public string? Reference { get; private set; }

    public string? DocumentNumber { get; private set; }
    public Guid? FiscalYearId { get; private set; }

    public EntryStatus Status { get; private set; } = EntryStatus.Draft;

    public decimal TotalDebit { get; private set; }

    public decimal TotalCredit { get; private set; }

    /// <summary>
    /// Set on the REVERSING entry: the entry it contrepasses. Unique in the database, which is
    /// what stops two concurrent reversals of the same entry from both succeeding.
    /// </summary>
    public Guid? ReversesEntryId { get; private set; }

    /// <summary>Set on the REVERSED entry: the reversing entry that corrected it.</summary>
    public Guid? ReversedByEntryId { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public string? PostedBy { get; private set; }

    public DateTimeOffset? ReversedAt { get; private set; }

    public string? ReversedBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    /// <summary>
    /// Amounts are capped at two decimals by <see cref="JournalEntryLine"/>, so exact decimal
    /// equality is the right comparison here - no epsilon, no rounding slack.
    /// </summary>
    public bool IsBalanced => TotalDebit == TotalCredit;

    public bool CanEdit => Status == EntryStatus.Draft;

    public bool IsReversed => ReversedByEntryId.HasValue;

    /// <summary>
    /// Replaces the whole set of lines and renumbers them from 1. Draft only: invariant 3.
    /// </summary>
    public void ReplaceLines(IReadOnlyCollection<JournalEntryLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        RequireDraft();

        _lines.Clear();

        var lineNumber = 1;

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);
            line.SetLineNumber(lineNumber++);
            _lines.Add(line);
        }

        RecalculateTotals();
    }

    /// <summary>
    /// Corrects the header of a draft. Draft only: invariant 3.
    /// </summary>
    public void UpdateHeader(DateOnly entryDate, string journalCode, string label, string? reference)
    {
        RequireDraft();

        EntryDate = entryDate;
        JournalCode = AccountingJournal.NormalizeCode(journalCode);
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        Reference = NormalizeOptional(reference, nameof(reference), MaxReferenceLength);
    }

    /// <summary>
    /// Enters the entry in the books. Invariants 1 and 2 are checked here and nowhere else.
    /// </summary>
    public void Post(string userName, DateTimeOffset utcNow)
    {
        if (Status != EntryStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft journal entry can be posted.");
        }

        if (_lines.Count < 2)
        {
            throw new InvalidOperationException(
                "A journal entry requires at least two lines to be posted (double entry).");
        }

        if (!IsBalanced)
        {
            throw new InvalidOperationException(
                $"A journal entry must be balanced to be posted: total debit is {TotalDebit} and " +
                $"total credit is {TotalCredit}. It stays a draft until the two sides match.");
        }

        Status = EntryStatus.Posted;
        PostedAt = utcNow;
        PostedBy = RequireActor(userName);
    }

    public void AssignDocumentNumber(Guid fiscalYearId, string documentNumber)
    {
        if (Status != EntryStatus.Draft && !(Status == EntryStatus.Posted && ReversesEntryId.HasValue))
            throw new InvalidOperationException("Only a draft or a newly-created reversal can receive its definitive number.");
        if (DocumentNumber is not null) throw new InvalidOperationException("The journal entry already has a definitive number.");
        FiscalYearId = fiscalYearId;
        DocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? throw new ArgumentException("Document number is required.", nameof(documentNumber)) : documentNumber.Trim();
    }

    /// <summary>
    /// Abandons a DRAFT that will never be posted. Refused on a posted entry: an entry that has
    /// entered the books is corrected by <see cref="CreateReversal"/>, never cancelled.
    /// </summary>
    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status != EntryStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft journal entry can be cancelled. A posted entry is corrected by a reversing entry.");
        }

        // Validate before mutating so a rejected reason leaves the entry untouched.
        var cancellationReason = RequireValue(reason, nameof(reason), 500);

        Status = EntryStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
        CancellationReason = cancellationReason;
    }

    /// <summary>
    /// Builds the reversing entry (extourne / contrepassation) of this posted entry and flags
    /// this one as reversed. The returned entry:
    /// <list type="bullet">
    ///   <item>is recorded in the SAME journal, so the correction sits next to what it corrects;</item>
    ///   <item>mirrors every line exactly - same account, same label, debit and credit swapped;</item>
    ///   <item>points back at this entry through <see cref="ReversesEntryId"/>;</item>
    ///   <item>is posted immediately: it is balanced by construction (swapping the two sides of a
    ///     balanced entry yields a balanced entry) and carries the same number of lines, so
    ///     invariants 1 and 2 hold by construction and leaving it a draft would only let someone
    ///     edit a correction that is already exact.</item>
    /// </list>
    /// The caller persists the returned entry; this one is left Posted, with
    /// <see cref="ReversedByEntryId"/> set. The net effect on the trial balance over a period
    /// covering both entries is exactly zero.
    /// </summary>
    /// <param name="reversalDate">
    /// Date to record the reversal on. Defaults to this entry's own date when null, which makes
    /// the two entries cancel out inside any period that contains the original.
    /// </param>
    public JournalEntry CreateReversal(
        DateOnly? reversalDate,
        string? reference,
        string userName,
        DateTimeOffset utcNow)
    {
        if (Status != EntryStatus.Posted)
        {
            throw new InvalidOperationException(
                "Only a posted journal entry can be reversed. A draft is edited or cancelled instead.");
        }

        if (IsReversed)
        {
            throw new InvalidOperationException("This journal entry has already been reversed.");
        }

        var reversal = new JournalEntry(
            reversalDate ?? EntryDate,
            JournalCode,
            BuildReversalLabel(Label),
            reference ?? Reference);

        reversal.ReversesEntryId = Id;

        reversal.ReplaceLines(_lines
            .OrderBy(line => line.LineNumber)
            .Select(line => line.Reverse())
            .ToList());

        reversal.Post(userName, utcNow);

        ReversedByEntryId = reversal.Id;
        ReversedAt = utcNow;
        ReversedBy = RequireActor(userName);

        return reversal;
    }

    private void RequireDraft()
    {
        if (Status == EntryStatus.Posted)
        {
            throw new InvalidOperationException(
                "A posted journal entry is immutable. Correct it with a reversing entry instead.");
        }

        if (Status != EntryStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft journal entry can be modified.");
        }
    }

    private void RecalculateTotals()
    {
        TotalDebit = _lines.Sum(line => line.Debit);
        TotalCredit = _lines.Sum(line => line.Credit);
    }

    private static string BuildReversalLabel(string originalLabel)
    {
        const string prefix = "Extourne - ";

        var label = prefix + originalLabel;

        return label.Length <= MaxLabelLength ? label : label[..MaxLabelLength];
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
