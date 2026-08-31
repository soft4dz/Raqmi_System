using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// Housekeeping operations (module 10.2): the room board, the daily cleaning sheet and the
/// planning of the teams around it, the inspection loop, and the minibar - price list plus the
/// consumptions it bills onto the guest folio.
///
/// The module owns ONE fact about a room: how clean it is. Everything it says about occupancy is
/// read from the reservations of the lodging module at query time and never stored, so the two
/// modules can never end up disagreeing about who sleeps where tonight.
/// </summary>
public interface IHousekeepingService
{
    // Room board ---------------------------------------------------------------------------

    /// <summary>
    /// Every ACTIVE room of one unit for one day, crossing the housekeeping condition with what
    /// the reservations expect of the room, plus the task of the day when one exists.
    /// </summary>
    Task<ApplicationResult<RoomBoardResponse>> GetRoomBoardAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Declares the condition of one room by hand - what a supervisor does to withdraw a room
    /// from service, or to put back a room the sheet got wrong. The row is created on the first
    /// declaration.
    /// </summary>
    Task<ApplicationResult<RoomConditionResponse>> SetRoomConditionAsync(
        Guid roomId,
        SetRoomConditionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // Tasks --------------------------------------------------------------------------------

    /// <summary>
    /// Tasks over [from, to] inclusive, optionally narrowed to one unit, one status or one
    /// attendant. Most recent service date first.
    /// </summary>
    Task<IReadOnlyCollection<HousekeepingTaskResponse>> ListTasksAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        HousekeepingTaskStatus? status,
        string? assignedTo,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HousekeepingTaskResponse>> GetTaskAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>Plans one task by hand, for what the day sheet cannot guess (a deep clean, a late request).</summary>
    Task<ApplicationResult<HousekeepingTaskResponse>> CreateTaskAsync(
        CreateHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds the sheet of the day from the reservations: a departure clean per room freed that
    /// day, a stayover service per room still occupied, and a refresh per vacant room left
    /// dirty. Rooms out of order are skipped. Idempotent per (room, date, type).
    /// </summary>
    Task<ApplicationResult<GenerateHousekeepingTasksResponse>> GenerateDayTasksAsync(
        GenerateHousekeepingTasksRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HousekeepingTaskResponse>> AssignTaskAsync(
        Guid id,
        AssignHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HousekeepingTaskResponse>> StartTaskAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>The attendant declares the room done; the room condition moves to Clean.</summary>
    Task<ApplicationResult<HousekeepingTaskResponse>> CompleteTaskAsync(
        Guid id,
        CompleteHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// The supervisor verdict: accepting moves the room to Inspected and closes the task,
    /// refusing moves it back to Dirty and sends the task back to work with a mandatory reason.
    /// </summary>
    Task<ApplicationResult<HousekeepingTaskResponse>> InspectTaskAsync(
        Guid id,
        InspectHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HousekeepingTaskResponse>> CancelTaskAsync(
        Guid id,
        CancelHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>The planning view of one day: the load of each attendant and what is still unassigned.</summary>
    Task<ApplicationResult<HousekeepingDaySheetResponse>> GetDaySheetAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken);

    // Minibar ------------------------------------------------------------------------------

    Task<IReadOnlyCollection<MinibarItemResponse>> ListMinibarItemsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<MinibarItemResponse>> CreateMinibarItemAsync(
        CreateMinibarItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<MinibarItemResponse>> UpdateMinibarItemAsync(
        Guid id,
        UpdateMinibarItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<MinibarItemResponse>> SetMinibarItemActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MinibarConsumptionResponse>> ListMinibarConsumptionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        Guid? reservationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a consumption AND bills it on the folio of the checked-in stay, in a single
    /// database transaction: the housekeeping trace and the money are written together, or
    /// neither is. The folio line references the consumption row, so a disputed line on a bill
    /// leads back to who recorded it and when.
    /// </summary>
    Task<ApplicationResult<MinibarConsumptionResponse>> RecordMinibarConsumptionAsync(
        RecordMinibarConsumptionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
