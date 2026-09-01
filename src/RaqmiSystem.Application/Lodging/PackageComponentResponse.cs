using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une composante d'un forfait. <paramref name="Amount"/> n'est pas un prix de vente mais une
/// VENTILATION : le client paie le prix global, ce montant dit a quel service en attribuer la
/// recette.
/// </summary>
public sealed record PackageComponentResponse(
    string Label,
    decimal Amount,
    ChargeKind ChargeKind,
    string? ExtraCode,
    ExtraPricingBasis PricingBasis);
