namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// Le montant d'une catégorie de recettes pour une journée d'exploitation. Entité fille avec sa
/// propre table et une clé étrangère obligatoire vers la recette, sur le modèle de
/// <c>BudgetLine</c> : la configuration snake_case, l'index unique (daily_revenue_id,
/// category_code) et les contraintes restent explicites, et la ligne porte un Id stable.
///
/// Une ligne ne se crée et ne se modifie qu'à travers sa recette (<see cref="DailyRevenue"/>),
/// qui seule connaît le statut du workflow : c'est ce qui garantit qu'une recette soumise ou
/// validée ne peut plus voir ses montants bouger.
/// </summary>
public sealed class DailyRevenueLine
{
    private DailyRevenueLine()
    {
    }

    public DailyRevenueLine(string categoryCode, decimal amount)
    {
        CategoryCode = RevenueCategoryCodes.Normalize(categoryCode, nameof(categoryCode));
        Amount = RequireAmount(amount, nameof(amount));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid DailyRevenueId { get; private set; }

    public string CategoryCode { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    internal void UpdateAmount(decimal amount)
    {
        Amount = RequireAmount(amount, nameof(amount));
    }

    // Même règle que les anciennes colonnes : positif ou nul. Le montant nul est accepté ici pour
    // que la validation précède le filtrage (une recette à -1 doit être refusée, pas ignorée) ;
    // c'est la recette qui décide ensuite qu'un montant nul ne mérite pas de ligne.
    private static decimal RequireAmount(decimal amount, string argumentName)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, amount, "Value cannot be negative.");
        }

        return amount;
    }
}
