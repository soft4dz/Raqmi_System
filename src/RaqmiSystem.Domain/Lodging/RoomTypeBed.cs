namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une ligne du couchage STANDARD d'un type de chambre : "2 lits simples", "1 lit king".
/// L'ensemble des lignes forme la composition par defaut, dont heritent toutes les chambres du
/// type qui ne la surchargent pas.
/// </summary>
public sealed class RoomTypeBed
{
    /// <summary>Au-dela, c'est un dortoir ou une faute de frappe, pas une chambre d'hotel.</summary>
    public const int MaxQuantity = 20;

    private RoomTypeBed()
    {
    }

    public RoomTypeBed(BedType bedType, int quantity)
    {
        BedType = bedType;
        Quantity = RequireQuantity(quantity);
    }

    /// <summary>
    /// Identifiant auto-attribue : la configuration EF DOIT declarer ValueGeneratedNever(), sans
    /// quoi une ligne ajoutee a un type deja persiste serait marquee Modified au lieu d'Added.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid RoomTypeId { get; private set; }

    public BedType BedType { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Personnes couchees par cette ligne.</summary>
    public int Sleeps => BedType.Sleeps() * Quantity;

    internal static int RequireQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Le nombre de lits doit etre strictement positif.");
        }

        if (quantity > MaxQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"Le nombre de lits ne peut pas depasser {MaxQuantity}.");
        }

        return quantity;
    }
}
