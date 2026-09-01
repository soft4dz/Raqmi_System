using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Ce qui commande l'INVENTAIRE avant toute vente : blocages de chambres (OOO/OOS), regles
/// d'exploitation de l'unite, restrictions de vente et autorisations de surreservation.
///
/// Ces quatre familles sont regroupees parce qu'elles repondent a la meme question - "que puis-je
/// vendre, et jusqu'ou" - et parce qu'elles sont lues ENSEMBLE a chaque recherche de disponibilite.
/// </summary>
public interface ILodgingInventoryService
{
    // ----------------------------------- Blocages OOO / OOS -----------------------------------

    Task<ApplicationResult<IReadOnlyCollection<RoomBlockResponse>>> ListRoomBlocksAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        RoomBlockKind? kind,
        bool includeClosed,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomBlockResponse>> GetRoomBlockAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Bloque une chambre. Refuse quand un sejour non annule occupe la chambre sur la periode :
    /// bloquer une chambre habitee mettrait le client dehors sans que personne ne le sache.
    /// </summary>
    Task<ApplicationResult<RoomBlockResponse>> CreateRoomBlockAsync(
        string hotelUnitCode,
        CreateRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomBlockResponse>> UpdateRoomBlockAsync(
        Guid id,
        UpdateRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>Remet la chambre en service et informe le housekeeping.</summary>
    Task<ApplicationResult<RoomBlockResponse>> CloseRoomBlockAsync(
        Guid id,
        CloseRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomBlockResponse>> CancelRoomBlockAsync(
        Guid id,
        CancelRoomBlockRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------------ Politique d'unite ------------------------------------

    Task<ApplicationResult<LodgingPolicyResponse>> GetPolicyAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<LodgingPolicyResponse>> SavePolicyAsync(
        string hotelUnitCode,
        SaveLodgingPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // -------------------------------------- Restrictions --------------------------------------

    Task<ApplicationResult<IReadOnlyCollection<RateRestrictionResponse>>> ListRestrictionsAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RateRestrictionResponse>> CreateRestrictionAsync(
        SaveRateRestrictionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RateRestrictionResponse>> UpdateRestrictionAsync(
        Guid id,
        SaveRateRestrictionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RateRestrictionResponse>> SetRestrictionActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------------- Surreservation -------------------------------------

    Task<ApplicationResult<IReadOnlyCollection<OverbookingAllowanceResponse>>> ListOverbookingAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<OverbookingAllowanceResponse>> CreateOverbookingAsync(
        SaveOverbookingAllowanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<OverbookingAllowanceResponse>> UpdateOverbookingAsync(
        Guid id,
        SaveOverbookingAllowanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<OverbookingAllowanceResponse>> SetOverbookingActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);
}
