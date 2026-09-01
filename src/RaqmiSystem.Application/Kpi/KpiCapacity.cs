namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// La capacite d'un perimetre sur une periode, deja calculee par le calculateur d'hebergement.
///
/// Elle est passee aux autres calculateurs plutot que recalculee par chacun : le GOPPAR, le
/// CPOR et les couts salariaux par chambre divisent tous par le meme denominateur, et deux
/// implementations de "nuitees disponibles" dans le meme produit finiraient inevitablement par
/// diverger d'une chambre en travaux.
/// </summary>
public sealed record KpiCapacity(int AvailableNights, int OccupiedNights)
{
    public static KpiCapacity Empty { get; } = new(0, 0);
}
