using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une facture de vente. L'age d'une creance court depuis la DATE DE FACTURE et non depuis une
/// echeance : Raqmi System n'en porte pas, et le module Creances applique deja cette regle - le
/// moteur KPI la reprend sans la redefinir.
/// </summary>
public sealed record KpiInvoiceFact(
    string HotelUnitCode,
    string CustomerCode,
    DateOnly InvoiceDate,
    decimal TotalInclVat,
    InvoiceStatus Status);
