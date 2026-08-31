namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une ligne du couchage d'UNE chambre precise, quand celle-ci s'ecarte de son type : la 101 est
/// en lit double alors que le type "Double standard" est declare en deux lits simples.
///
/// L'absence de ligne signifie "cette chambre suit son type". Aucun indicateur separe ne le dit :
/// deux informations pour un meme fait finiraient par se contredire.
/// </summary>
public sealed class RoomBed
{
    private RoomBed()
    {
    }

    public RoomBed(BedType bedType, int quantity)
    {
        BedType = bedType;
        Quantity = RoomTypeBed.RequireQuantity(quantity);
    }

    /// <summary>Identifiant auto-attribue : ValueGeneratedNever() obligatoire, voir RoomTypeBed.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid RoomId { get; private set; }

    public BedType BedType { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Personnes couchees par cette ligne.</summary>
    public int Sleeps => BedType.Sleeps() * Quantity;
}
