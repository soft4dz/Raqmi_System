namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// One line of a journal entry: an amount posted to an account, on ONE side of the ledger.
///
/// The defining invariant, enforced in the constructor so no line can ever exist without it:
/// a line carries a debit OR a credit, never both and never neither. A line with two amounts is
/// not a line, it is two lines; a line with no amount is not an accounting fact at all. The
/// database restates the same rule as a check constraint (see JournalEntryLineConfiguration) so
/// data written outside this type cannot break it either.
/// </summary>
public sealed class JournalEntryLine
{
    public const int MaxLabelLength = 300;

    private JournalEntryLine()
    {
    }

    public JournalEntryLine(string accountCode, string label, decimal debit, decimal credit)
    {
        AccountCode = ChartAccount.NormalizeCode(accountCode);
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        Debit = RequireAmount(debit, nameof(debit));
        Credit = RequireAmount(credit, nameof(credit));

        if (Debit > 0 && Credit > 0)
        {
            throw new ArgumentException(
                "An accounting line carries either a debit or a credit, never both.",
                nameof(debit));
        }

        if (Debit == 0 && Credit == 0)
        {
            throw new ArgumentException(
                "An accounting line must carry a non-zero debit or a non-zero credit.",
                nameof(debit));
        }
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid JournalEntryId { get; private set; }

    public int LineNumber { get; private set; }

    /// <summary>Code of the <see cref="ChartAccount"/> the amount is posted to.</summary>
    public string AccountCode { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// The mirror of this line: same account, same label, debit and credit swapped. This is what
    /// makes a reversing entry an exact contrepassation - see
    /// <see cref="JournalEntry.CreateReversal"/>. The swap preserves both the one-side-only
    /// invariant (exactly one of the two amounts was non-zero, and still is) and the entry's
    /// balance (total debit and total credit trade places).
    /// </summary>
    internal JournalEntryLine Reverse()
    {
        return new JournalEntryLine(AccountCode, Label, Credit, Debit);
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

    /// <summary>
    /// Amounts are positive-or-zero and carry at most two decimals: the side of the ledger is
    /// the sign, so a negative debit would be a credit written the wrong way and must be
    /// refused rather than silently reinterpreted. The two-decimal cap mirrors the numeric(18,2)
    /// columns - a finer value would be truncated at persistence time and the stored entry would
    /// no longer balance the way the user saw it balance on screen.
    /// </summary>
    private static decimal RequireAmount(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Amount cannot be negative.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", argumentName);
        }

        return value;
    }
}
