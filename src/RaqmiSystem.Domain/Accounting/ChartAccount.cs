using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// One account of the chart of accounts (plan comptable). The SCF codification is numeric and
/// hierarchical: the first digit is the class (1..7), and each further digit narrows the account
/// down inside its class. <see cref="AccountClass"/> is therefore DERIVED from
/// <see cref="Code"/> rather than stored independently - an account whose code and class
/// disagree is not merely invalid, it is unrepresentable.
///
/// <see cref="Kind"/> is entered by the caller and checked against the class (see
/// <see cref="AccountClassCatalog"/> for the rule and why three of the seven classes cannot
/// deduce it).
/// </summary>
public sealed class ChartAccount : AuditableEntity
{
    public const int MaxCodeLength = 12;

    public const int MaxLabelLength = 200;

    private ChartAccount()
    {
    }

    public ChartAccount(string code, string label, AccountKind kind)
    {
        Code = NormalizeCode(code);
        AccountClass = ExtractAccountClass(Code);
        Kind = RequireCoherentKind(AccountClass, kind);
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        IsActive = true;
    }

    /// <summary>Numeric SCF code, normalized (trimmed, digits only, first digit 1..7).</summary>
    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>SCF class 1..7, always equal to the first digit of <see cref="Code"/>.</summary>
    public int AccountClass { get; private set; }

    public AccountKind Kind { get; private set; }

    /// <summary>
    /// Deactivated accounts stay in the chart (posted entries keep referencing them for good)
    /// but can no longer be used on a new or edited journal entry.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The code is immutable: it carries the account's class and is referenced by every line
    /// already recorded against it. Only the label and the kind can be corrected.
    /// </summary>
    public void UpdateDetails(string label, AccountKind kind)
    {
        Label = RequireValue(label, nameof(label), MaxLabelLength);
        Kind = RequireCoherentKind(AccountClass, kind);
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
    /// Single source of truth for the account-code format: trimmed, ASCII digits only, 1 to
    /// <see cref="MaxCodeLength"/> characters, first digit in 1..7 (a code outside the seven
    /// classes has no place in an SCF chart). Exposed because journal entry lines reference
    /// accounts by code and must normalize them the very same way.
    /// </summary>
    public static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Account code is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Account code cannot exceed {MaxCodeLength} characters.", nameof(value));
        }

        if (!trimmed.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Account code must contain digits only.", nameof(value));
        }

        var accountClass = trimmed[0] - '0';

        if (accountClass is < AccountClassCatalog.MinAccountClass or > AccountClassCatalog.MaxAccountClass)
        {
            throw new ArgumentException(
                "Account code must start with an SCF class digit between 1 and 7.",
                nameof(value));
        }

        return trimmed;
    }

    /// <summary>
    /// The class of an already normalized code: its first digit.
    /// </summary>
    public static int ExtractAccountClass(string normalizedCode)
    {
        if (string.IsNullOrEmpty(normalizedCode))
        {
            throw new ArgumentException("Account code is required.", nameof(normalizedCode));
        }

        return normalizedCode[0] - '0';
    }

    /// <summary>
    /// Enforces the class/kind table of <see cref="AccountClassCatalog"/>: a class-6 account
    /// cannot be a revenue, a class-7 account cannot be an expense, and so on. Returns the kind
    /// when it is admissible so callers can assign the result directly.
    /// </summary>
    public static AccountKind RequireCoherentKind(int accountClass, AccountKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("Account kind is not valid.", nameof(kind));
        }

        var definition = AccountClassCatalog.Find(accountClass);

        if (definition is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(accountClass),
                accountClass,
                "Account class must be between 1 and 7.");
        }

        if (!definition.AllowedKinds.Contains(kind))
        {
            throw new ArgumentException(
                $"Account kind {kind} is not allowed for class {accountClass} ({definition.Label}). " +
                $"Allowed kinds: {string.Join(", ", definition.AllowedKinds)}.",
                nameof(kind));
        }

        return kind;
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
