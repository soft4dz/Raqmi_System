using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// One movement of the loyalty point ledger of one guest. The ledger is APPEND-ONLY: a movement
/// is never edited nor deleted, a mistake is corrected by an
/// <see cref="LoyaltyTransactionKind.Adjustment"/> that says so. The balance of a guest is the
/// sum of <see cref="Points"/> over their movements, which is the only definition of it in the
/// module.
///
/// <see cref="Points"/> is SIGNED, and its sign is imposed by the <see cref="Kind"/>: earning
/// adds, redeeming and expiry subtract, an adjustment does either but never nothing. That is what
/// lets the balance be a plain sum, and what stops a redemption from silently crediting points.
///
/// <see cref="Reason"/> is required on every movement, earnings included: a balance a guest can
/// dispute at the front desk has to be explainable movement by movement.
/// </summary>
public sealed class LoyaltyTransaction : AuditableEntity
{
    private LoyaltyTransaction()
    {
    }

    public LoyaltyTransaction(
        string customerCode,
        LoyaltyTransactionKind kind,
        int points,
        DateOnly occurredOn,
        string reason,
        string? reference = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown loyalty movement kind.");
        }

        RequireSignMatchingKind(kind, points, nameof(points));

        CustomerCode = Customer.NormalizeCode(customerCode);
        Kind = kind;
        Points = points;
        OccurredOn = occurredOn;
        Reason = CrmText.Require(reason, nameof(reason), 300);
        Reference = CrmText.Optional(reference, nameof(reference), 80);
    }

    public string CustomerCode { get; private set; } = string.Empty;

    public LoyaltyTransactionKind Kind { get; private set; }

    /// <summary>Signed number of points: positive credits the guest, negative debits them.</summary>
    public int Points { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Free reference to what justifies the movement in another module - a reservation, an
    /// invoice number. Free text rather than a foreign key: a movement must survive whatever it
    /// points at, and the ledger is not the place to enforce another module's identity.
    /// </summary>
    public string? Reference { get; private set; }

    /// <summary>
    /// The sign rule of the ledger, in one place. Exposed as a static so the service can hold a
    /// request to it before building the entity, and answer with a business message rather than
    /// letting the constructor throw.
    /// </summary>
    public static void RequireSignMatchingKind(LoyaltyTransactionKind kind, int points, string argumentName)
    {
        switch (kind)
        {
            case LoyaltyTransactionKind.Earn when points <= 0:
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    points,
                    "An earning movement must credit a strictly positive number of points.");

            case LoyaltyTransactionKind.Redeem when points >= 0:
            case LoyaltyTransactionKind.Expiry when points >= 0:
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    points,
                    "A redemption or an expiry must debit a strictly negative number of points.");

            case LoyaltyTransactionKind.Adjustment when points == 0:
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    points,
                    "An adjustment of zero point would move nothing.");
        }
    }
}
