using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// An accounting journal (journal comptable): the book an entry is recorded in. Usual codes in a
/// hotel establishment are VE (ventes), AC (achats), BQ (banque), CA (caisse) and OD (operations
/// diverses), but nothing is seeded - the establishment declares the journals it actually keeps
/// through <c>POST /accounting/journals</c>.
/// </summary>
public sealed class AccountingJournal : AuditableEntity
{
    public const int MaxCodeLength = 10;

    public const int MaxLabelLength = 200;

    private AccountingJournal()
    {
    }

    public AccountingJournal(string code, string label)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>
    /// A deactivated journal keeps every entry already recorded in it (and a posted entry can
    /// still be reversed inside it) but accepts no new one.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The code is immutable: entries reference their journal by code.
    /// </summary>
    public void UpdateDetails(string label)
    {
        Label = RequireValue(label, nameof(label), MaxLabelLength);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Single source of truth for the journal-code format: trimmed, upper-cased, letters and
    /// digits only, at most <see cref="MaxCodeLength"/> characters. Exposed because journal
    /// entries carry the code and must normalize it identically.
    /// </summary>
    public static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Journal code is required.", nameof(value));
        }

        var trimmed = value.Trim().ToUpperInvariant();

        if (trimmed.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Journal code cannot exceed {MaxCodeLength} characters.", nameof(value));
        }

        if (!trimmed.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Journal code must contain letters and digits only.", nameof(value));
        }

        return trimmed;
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
}
