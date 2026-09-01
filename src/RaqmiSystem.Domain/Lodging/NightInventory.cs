namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// L'inventaire d'UN type de chambre pour UNE nuit, decompose. C'est la brique elementaire de tout
/// le PMS : la recherche de disponibilite, le garde de creation, le forecast, le yield et le
/// channel manager lisent tous cette meme decomposition, jamais leur propre soustraction.
///
/// L'ORDRE DES SOUSTRACTIONS N'EST PAS ARBITRAIRE :
///   parc physique
///     - chambres bloquees (hors service technique, et hors service d'exploitation selon la
///       politique de l'unite)
///     = capacite vendable
///     - chambres deja vendues
///     = disponible physique
///     - chambres tenues pour des groupes
///     = disponible a la vente publique
///     + solde de surreservation autorise
///     = disponible commercial
///
/// Le lire dans un autre ordre donne des resultats faux : soustraire les allotements avant les
/// blocages, par exemple, ferait disparaitre deux fois la meme chambre le jour ou un bloc porte sur
/// une chambre en panne.
/// </summary>
public sealed record NightInventory(
    DateOnly Night,
    int PhysicalRooms,
    int BlockedRooms,
    int SoldRooms,
    int AllotmentHolds,
    int OverbookingAllowed)
{
    /// <summary>Chambres reellement exploitables cette nuit : le parc moins les blocages.</summary>
    public int SellableCapacity => Math.Max(0, PhysicalRooms - BlockedRooms);

    /// <summary>Chambres vendues au-dela de la capacite physique. Zero en exploitation normale.</summary>
    public int OverbookingUsed => Math.Max(0, SoldRooms - SellableCapacity);

    /// <summary>Chambres physiquement libres, avant prise en compte des groupes.</summary>
    public int PhysicalAvailable => Math.Max(0, SellableCapacity - SoldRooms);

    /// <summary>
    /// Chambres vendables au public : le disponible physique moins ce que les groupes tiennent
    /// encore. C'est ce nombre, et lui seul, qu'une vente publique peut consommer.
    /// </summary>
    public int PublicAvailable => Math.Max(0, PhysicalAvailable - AllotmentHolds);

    /// <summary>Solde de surreservation encore ouvert, apres deduction de celui deja consomme.</summary>
    public int OverbookingRemaining => Math.Max(0, OverbookingAllowed - OverbookingUsed);

    /// <summary>
    /// Disponible commercial : ce que l'hotel accepte de vendre, surreservation comprise. C'est le
    /// chiffre affiche au commercial, jamais celui utilise pour affecter une chambre physique.
    /// </summary>
    public int CommercialAvailable => PublicAvailable + OverbookingRemaining;

    /// <summary>Vrai quand vendre une chambre de plus franchirait la capacite physique.</summary>
    public bool NextSaleIsOverbooking => PublicAvailable == 0;

    /// <summary>Taux d'occupation de la nuit, sur la capacite vendable. Zero quand rien n'est exploitable.</summary>
    public decimal OccupancyPercent => SellableCapacity == 0
        ? 0m
        : Math.Round((decimal)SoldRooms * 100m / SellableCapacity, 2, MidpointRounding.AwayFromZero);

    /// <summary>Additionne deux inventaires de la meme nuit : sert a agreger les types d'une unite.</summary>
    public static NightInventory operator +(NightInventory left, NightInventory right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Night != right.Night)
        {
            throw new ArgumentException("Deux inventaires de nuits differentes ne s'additionnent pas.", nameof(right));
        }

        return new NightInventory(
            left.Night,
            left.PhysicalRooms + right.PhysicalRooms,
            left.BlockedRooms + right.BlockedRooms,
            left.SoldRooms + right.SoldRooms,
            left.AllotmentHolds + right.AllotmentHolds,
            left.OverbookingAllowed + right.OverbookingAllowed);
    }
}
