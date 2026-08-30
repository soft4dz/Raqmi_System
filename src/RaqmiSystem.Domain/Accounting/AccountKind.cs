namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// Nature of a chart-of-accounts account, i.e. which of the five financial-statement categories
/// its balance ends up in. The SCF (Systeme Comptable Financier, in force in Algeria since 2010)
/// is aligned on the IFRS conceptual framework, whose elements are exactly these five.
///
/// The kind is NOT freely combinable with the account class: see
/// <see cref="AccountClassCatalog"/> for the class/kind table that
/// <see cref="ChartAccount.RequireCoherentKind"/> enforces.
/// </summary>
public enum AccountKind
{
    /// <summary>Actif - a resource controlled by the entity.</summary>
    Asset = 1,

    /// <summary>Passif (dette) - a present obligation of the entity.</summary>
    Liability = 2,

    /// <summary>Capitaux propres - the residual interest in the assets after deducting liabilities.</summary>
    Equity = 3,

    /// <summary>Produit - income of the period.</summary>
    Revenue = 4,

    /// <summary>Charge - expense of the period.</summary>
    Expense = 5
}
