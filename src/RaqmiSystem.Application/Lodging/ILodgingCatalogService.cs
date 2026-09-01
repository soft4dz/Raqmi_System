using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Les referentiels commerciaux du PMS : extras, forfaits, politiques d'annulation et regles de
/// revenue management. Ils se parametrent, ils ne se vendent pas directement - c'est la reservation
/// qui les consomme.
/// </summary>
public interface ILodgingCatalogService
{
    // ----------------------------------------- Extras -----------------------------------------

    Task<ApplicationResult<IReadOnlyCollection<ExtraItemResponse>>> ListExtrasAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ExtraItemResponse>> CreateExtraAsync(
        SaveExtraItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ExtraItemResponse>> UpdateExtraAsync(
        Guid id,
        SaveExtraItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ExtraItemResponse>> SetExtraActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ---------------------------------------- Forfaits ----------------------------------------

    Task<ApplicationResult<IReadOnlyCollection<PackageResponse>>> ListPackagesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PackageResponse>> CreatePackageAsync(
        SavePackageRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PackageResponse>> UpdatePackageAsync(
        Guid id,
        SavePackageRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PackageResponse>> SetPackageActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------- Politiques d'annulation -------------------------------

    Task<ApplicationResult<IReadOnlyCollection<CancellationPolicyResponse>>> ListCancellationPoliciesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CancellationPolicyResponse>> CreateCancellationPolicyAsync(
        SaveCancellationPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CancellationPolicyResponse>> UpdateCancellationPolicyAsync(
        Guid id,
        SaveCancellationPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CancellationPolicyResponse>> SetCancellationPolicyActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // --------------------------------- Revenue management ---------------------------------

    Task<ApplicationResult<IReadOnlyCollection<YieldRuleResponse>>> ListYieldRulesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<YieldRuleResponse>> CreateYieldRuleAsync(
        SaveYieldRuleRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<YieldRuleResponse>> UpdateYieldRuleAsync(
        Guid id,
        SaveYieldRuleRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<YieldRuleResponse>> SetYieldRuleActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);
}
