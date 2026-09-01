namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Le planning graphique : une ligne par chambre, une colonne par jour, des blocs pour ce qui
/// occupe la chambre.
///
/// <paramref name="UnassignedStays"/> porte les sejours vendus sans chambre affectee. Ils n'ont
/// aucune ligne sur le plan et pourtant ils CONSOMMENT l'inventaire : les omettre ferait croire a
/// des chambres libres qui sont deja vendues, ce qui est exactement la facon dont un tape chart
/// fait survendre un hotel.
/// </summary>
public sealed record TapeChartResponse(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<TapeChartRowResponse> Rows,
    IReadOnlyCollection<TapeChartBarResponse> UnassignedStays,
    IReadOnlyCollection<NightInventoryResponse> DailyInventory);
