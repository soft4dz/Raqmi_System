namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Prolongation ou raccourcissement d'un sejour. La nouvelle periode est revalidee entierement -
/// disponibilite, allotements, restrictions, surreservation - puis les tarifs sont reposes nuit par
/// nuit : sans ce repricing, les nuits ajoutees seraient facturees a zero.
/// </summary>
public sealed record ExtendStayRequest(
    DateOnly DepartureDate,
    string? Reason = null,
    bool AllowOverbooking = false,
    bool OverrideRestrictions = false);
