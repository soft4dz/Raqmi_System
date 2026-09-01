using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>Un rattachement de comptes configure, tel que l'API le rend.</summary>
public sealed record KpiAccountMappingResponse(
    Guid Id,
    string AccountPrefix,
    KpiAccountGroup Group,
    string Label,
    bool IsActive);
