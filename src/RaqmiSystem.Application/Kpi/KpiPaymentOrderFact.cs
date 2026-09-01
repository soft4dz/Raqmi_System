using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un ordre de paiement. Il ne porte PAS d'unite hoteliere dans Raqmi System : c'est la raison
/// pour laquelle les decaissements, le flux de tresorerie et les engagements a echeance
/// n'existent qu'au niveau du groupe (voir <c>KpiScopeLevel.GroupOnly</c>).
///
/// <paramref name="PaidOn"/> est la date de reglement effectif quand elle existe : elle seule
/// dit quand l'argent est sorti, la date d'ordre n'etant qu'une date de saisie.
/// </summary>
public sealed record KpiPaymentOrderFact(
    DateOnly OrderDate,
    DateOnly DueDate,
    DateOnly? PaidOn,
    decimal Amount,
    PaymentOrderStatus Status);
