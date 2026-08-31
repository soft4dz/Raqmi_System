namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// Why a loyalty movement exists. The kind CONSTRAINS THE SIGN of the movement - see
/// <see cref="LoyaltyTransaction"/> - so a ledger read back months later still says whether
/// points were won, spent or corrected, and not merely that the balance moved.
/// </summary>
public enum LoyaltyTransactionKind
{
    /// <summary>Points won by the guest (a stay, a spend). Strictly positive.</summary>
    Earn = 0,

    /// <summary>Points spent by the guest against a benefit. Strictly negative.</summary>
    Redeem = 1,

    /// <summary>Manual correction by a manager, either way. Never zero.</summary>
    Adjustment = 2,

    /// <summary>Points written off because they aged out of the programme. Strictly negative.</summary>
    Expiry = 3
}
