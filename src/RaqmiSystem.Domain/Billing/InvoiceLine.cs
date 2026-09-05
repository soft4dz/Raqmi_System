namespace RaqmiSystem.Domain.Billing;

/// <summary>
/// Invoice line, modelled as a child entity with its own table and a required FK to
/// <see cref="Invoice"/> (rather than an EF owned collection): a dedicated entity keeps the
/// snake_case table configuration, named indexes and check constraints explicit and consistent
/// with every other configuration in this repository, and lets lines carry a stable Id that can
/// be referenced from API responses.
/// </summary>
public sealed class InvoiceLine
{
    /// <summary>
    /// Algerian VAT rates in force: exempt, reduced and standard.
    /// </summary>
    public static readonly IReadOnlyCollection<decimal> AllowedVatRates = new[] { 0m, 9m, 19m };

    private InvoiceLine()
    {
    }

    /// <param name="vatAmount">
    /// TVA FIGEE de la ligne, pour une ligne construite depuis un montant TTC (ligne de folio,
    /// ticket de caisse) : la TVA y a ete extraite du TTC, et la recalculer depuis le HT arrondi
    /// peut s'en ecarter d'un centime - ce qui ferait facturer 3 999,99 pour un diner paye 4 000.
    /// Null (le cas general) : la TVA est calculee HT x taux. Une TVA figee ne peut s'ecarter du
    /// calcul que d'UN centime : au-dela ce n'est plus un arrondi, c'est une erreur.
    /// </param>
    public InvoiceLine(
        string designation,
        decimal quantity,
        decimal unitPrice,
        decimal vatRate,
        string? articleCode = null,
        decimal? vatAmount = null)
    {
        Designation = RequireValue(designation, nameof(designation), 300);
        ArticleCode = NormalizeArticleCode(articleCode);
        Quantity = RequireMaxScale(RequireStrictlyPositive(quantity, nameof(quantity)), 3, nameof(quantity));
        UnitPrice = RequireMaxScale(RequirePositiveOrZero(unitPrice, nameof(unitPrice)), 2, nameof(unitPrice));
        VatRate = RequireAllowedVatRate(vatRate, nameof(vatRate));
        LineTotalExclVat = RoundMoney(Quantity * UnitPrice);
        VatAmount = vatAmount is { } pinned
            ? RequirePinnedVatAmount(pinned, ComputeVatAmount(LineTotalExclVat, VatRate))
            : ComputeVatAmount(LineTotalExclVat, VatRate);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InvoiceId { get; private set; }

    public int LineNumber { get; private set; }

    public string Designation { get; private set; } = string.Empty;

    /// <summary>
    /// Code de l'article du catalogue dont la ligne est issue, quand il y en a un. Nullable par
    /// construction : une ligne libre (prestation ponctuelle, facture d'evenement, ligne de
    /// folio) reste legitime, et les factures anterieures au catalogue n'en portent aucun.
    /// La designation, le prix et le taux restent portes par la ligne elle-meme : ils ont ete
    /// repris de l'article au moment de la saisie et n'en suivent plus les modifications - une
    /// facture emise ne change pas parce que le tarif du catalogue a change.
    /// </summary>
    public string? ArticleCode { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal VatRate { get; private set; }

    public decimal LineTotalExclVat { get; private set; }

    /// <summary>
    /// TVA de la ligne, STOCKEE et non recalculee : c'est un montant legal, celui que porte la
    /// facture emise. Egale a HT x taux arrondi au centime, sauf pour une ligne construite depuis
    /// un TTC dont la TVA a ete figee a la construction (voir le constructeur).
    /// </summary>
    public decimal VatAmount { get; private set; }

    public decimal LineTotalInclVat => LineTotalExclVat + VatAmount;

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    internal static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>La regle de calcul de la TVA d'une ligne, en un seul endroit : HT x taux, arrondi au centime.</summary>
    public static decimal ComputeVatAmount(decimal lineTotalExclVat, decimal vatRate)
    {
        return RoundMoney(lineTotalExclVat * vatRate / 100m);
    }

    private static decimal RequirePinnedVatAmount(decimal pinned, decimal computed)
    {
        if (pinned < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(pinned), pinned, "VAT amount cannot be negative.");
        }

        if (decimal.Round(pinned, 2) != pinned)
        {
            throw new ArgumentException("VAT amount cannot have more than 2 decimal places.", nameof(pinned));
        }

        if (Math.Abs(pinned - computed) > 0.01m)
        {
            throw new ArgumentException(
                $"A pinned VAT amount may differ from the computed amount ({computed}) by at most one cent.",
                nameof(pinned));
        }

        return pinned;
    }

    /// <summary>
    /// Single source of truth for the Algerian VAT rate rule. Exposed because the rate is also
    /// carried outside a line - <c>ApplicationSettings.DefaultVatRate</c> pre-fills new lines with
    /// it, and a default the line constructor would then refuse would be a trap. Both sides must
    /// validate against the very same list, so neither may restate the rule.
    /// </summary>
    public static decimal RequireAllowedVatRate(decimal vatRate, string argumentName)
    {
        if (!AllowedVatRates.Contains(vatRate))
        {
            throw new ArgumentException("VAT rate must be 0, 9 or 19.", argumentName);
        }

        return vatRate;
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
    /// Meme normalisation que le code du catalogue (majuscules, 40 caracteres) sans en dependre :
    /// le domaine Facturation ne connait pas le catalogue, il porte seulement la cle qui y renvoie.
    /// </summary>
    private static string? NormalizeArticleCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static decimal RequireStrictlyPositive(decimal value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }

    private static decimal RequirePositiveOrZero(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }

    /// <summary>
    /// The database columns store quantity with 3 decimals and unit price with 2; a value with
    /// more precision would be silently truncated at persistence time and the stored line
    /// total would no longer match the amount the user validated on screen - refuse it upfront.
    /// </summary>
    private static decimal RequireMaxScale(decimal value, int maxScale, string argumentName)
    {
        if (decimal.Round(value, maxScale) != value)
        {
            throw new ArgumentException($"Value cannot have more than {maxScale} decimal places.", argumentName);
        }

        return value;
    }
}
