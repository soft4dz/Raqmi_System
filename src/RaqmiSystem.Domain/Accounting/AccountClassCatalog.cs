namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// The STRUCTURE of the Algerian SCF chart of accounts: its seven classes, their headings, and
/// the account kinds each class may carry.
///
/// WHAT THIS IS NOT: this catalog deliberately does NOT ship a chart of accounts. The SCF's
/// numbered accounts (the two-, three- and four-digit accounts inside each class) are a
/// regulatory list; reproducing one from memory would present invented codes as a legal
/// reference, and an accountant reading it would have no way to tell the invented entries from
/// the real ones. So the module ships the class skeleton - which is stable, short, and
/// verifiable - and the accounts themselves are DATA, entered through
/// <c>POST /accounting/accounts</c> and validated by the establishment's own accountant against
/// the official nomenclature. No starter account set is seeded anywhere in this module.
///
/// The class headings below are the seven SCF class titles. The codification is numeric: an
/// account's class is the first digit of its code (see <see cref="ChartAccount"/>), which is why
/// <see cref="ChartAccount.AccountClass"/> is derived from the code rather than entered
/// separately - a code and a class that disagree cannot be represented at all.
///
/// CLASS/KIND RULE (the one enforced by <see cref="ChartAccount.RequireCoherentKind"/>): four
/// classes determine the kind unambiguously (2 and 3 are assets, 6 is expenses, 7 is revenue),
/// and three do not, so for those the kind must be entered and is merely checked against the
/// short list of possibilities:
/// <list type="bullet">
///   <item>class 1 holds both equity (capital, reserves, result) and long-term debt;</item>
///   <item>class 4 holds both receivables (customers) and payables (suppliers, tax, payroll);</item>
///   <item>class 5 holds cash and bank accounts, which can be debtor or creditor (overdraft).</item>
/// </list>
/// </summary>
public static class AccountClassCatalog
{
    public const int MinAccountClass = 1;

    public const int MaxAccountClass = 7;

    public static IReadOnlyCollection<AccountClassDefinition> All { get; } = new[]
    {
        new AccountClassDefinition(1, "Comptes de capitaux", new[] { AccountKind.Equity, AccountKind.Liability }),
        new AccountClassDefinition(2, "Comptes d'immobilisations", new[] { AccountKind.Asset }),
        new AccountClassDefinition(3, "Comptes de stocks et en-cours", new[] { AccountKind.Asset }),
        new AccountClassDefinition(4, "Comptes de tiers", new[] { AccountKind.Asset, AccountKind.Liability }),
        new AccountClassDefinition(5, "Comptes financiers", new[] { AccountKind.Asset, AccountKind.Liability }),
        new AccountClassDefinition(6, "Comptes de charges", new[] { AccountKind.Expense }),
        new AccountClassDefinition(7, "Comptes de produits", new[] { AccountKind.Revenue })
    };

    /// <summary>
    /// Returns the definition of the given class, or null when the number is outside 1..7.
    /// </summary>
    public static AccountClassDefinition? Find(int accountClass)
    {
        return All.SingleOrDefault(definition => definition.AccountClass == accountClass);
    }

    /// <summary>
    /// Official heading of the class, or null when the number is outside 1..7. Used to decorate
    /// API responses so a chart-of-accounts screen never has to restate the headings itself.
    /// </summary>
    public static string? LabelOf(int accountClass)
    {
        return Find(accountClass)?.Label;
    }
}
