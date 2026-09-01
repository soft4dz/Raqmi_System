using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Globalization;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Housekeeping;

/// <summary>
/// Housekeeping service (module 10.2): room board, daily sheet and team planning, inspection
/// loop, and the minibar with the folio lines it produces.
///
/// TWO AXES, ONE OWNER EACH. The module owns the CLEANLINESS of a room
/// (<see cref="RoomCondition"/>). It owns nothing about OCCUPANCY: whether a room is occupied,
/// freed or taken tonight is read from the reservations at query time
/// (<see cref="ClassifyOccupancy"/>). Copying that into a housekeeping table would create a
/// second truth about the same fact, free to drift the day somebody cancels a booking without
/// telling the floor.
///
/// MINIBAR AND THE FOLIO. Recording a consumption must never leave a guest billed for a line no
/// housekeeping record explains, nor a housekeeping record for money nobody was charged. The
/// consumption row is therefore ADDED TO THE CONTEXT BUT NOT SAVED, and
/// <see cref="ILodgingService.AddFolioChargeAsync"/> is called next: its own Serializable
/// transaction flushes the pending row together with the folio line, so both land or neither
/// does. That reuses the folio discipline of the lodging module (checked-in guard, atomic claim
/// against a racing check-out) instead of re-implementing it here, where it would be free to
/// drift.
/// </summary>
public sealed class HousekeepingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ILodgingService lodgingService) : IHousekeepingService
{
    private const string TasksEntity = "housekeeping.tasks";

    private const string ConditionsEntity = "housekeeping.room_conditions";

    private const string MinibarItemsEntity = "housekeeping.minibar_items";

    private const string MinibarConsumptionsEntity = "housekeeping.minibar_consumptions";

    /// <summary>
    /// A task list is read to plan a shift, not to mine a year of history; an unbounded window
    /// would turn one request into an arbitrary amount of work.
    /// </summary>
    private const int MaxTaskWindowDays = 92;

    private const string ConcurrentTaskMutationRefused =
        "This task was just modified by a concurrent operation. Reload the sheet and try again.";

    // Room board ---------------------------------------------------------------------------

    public async Task<ApplicationResult<RoomBoardResponse>> GetRoomBoardAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var unit = await RequireActiveHotelUnitAsync<RoomBoardResponse>(hotelUnitCode, cancellationToken);

        if (unit.Failure is not null)
        {
            return unit.Failure;
        }

        var rooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.HotelUnitCode == unit.UnitCode && room.IsActive)
            .OrderBy(room => room.Number)
            .ToArrayAsync(cancellationToken);

        var roomIds = rooms.Select(room => room.Id).ToArray();

        var conditions = await dbContext.Set<RoomCondition>()
            .AsNoTracking()
            .Where(condition => roomIds.Contains(condition.RoomId))
            .ToDictionaryAsync(condition => condition.RoomId, cancellationToken);

        // Every stay that touches the day: it either covers the night [date, date+1), or it ends
        // that morning. A departure is still "about" the room on its departure day even though
        // it no longer occupies the night - that is precisely the room that needs the full clean.
        var nextDay = date.AddDays(1);

        var stays = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.RoomId != null
                && roomIds.Contains(reservation.RoomId.Value)
                && reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate < nextDay
                && reservation.DepartureDate >= date)
            .ToArrayAsync(cancellationToken);

        // Le regroupement porte sur les sejours AFFECTES : depuis que le PMS vend par type, un
        // dossier peut n'avoir aucune chambre, et une feuille de menage ne s'ecrit que pour une
        // chambre reelle. Les dossiers sans chambre sont ecartes ici, ils reviendront quand la
        // reception aura affecte.
        var staysByRoom = stays
            .Where(reservation => reservation.RoomId is not null)
            .GroupBy(reservation => reservation.RoomId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var tasks = await dbContext.Set<HousekeepingTask>()
            .AsNoTracking()
            .Where(task => task.HotelUnitCode == unit.UnitCode
                && task.ServiceDate == date
                && task.Status != HousekeepingTaskStatus.Cancelled)
            .ToArrayAsync(cancellationToken);

        // One room can carry a departure clean AND a deep clean the same day. The board shows
        // the one that still needs attention, so the head housekeeper sees the open work rather
        // than a task already signed off hours ago.
        var taskByRoom = tasks
            .GroupBy(task => task.RoomId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(task => task.Status == HousekeepingTaskStatus.Inspected ? 1 : 0)
                    .ThenBy(task => task.TaskType)
                    .First());

        var rows = new List<RoomBoardRow>(rooms.Length);

        foreach (var room in rooms)
        {
            var condition = conditions.GetValueOrDefault(room.Id);
            var roomStays = staysByRoom.GetValueOrDefault(room.Id, []);
            var occupancy = ClassifyOccupancy(roomStays, date);
            var current = PickRepresentativeStay(roomStays, date);
            var task = taskByRoom.GetValueOrDefault(room.Id);

            rows.Add(new RoomBoardRow(
                room.Id,
                room.Number,
                room.RoomTypeCode,
                room.Floor,
                // A room nobody ever declared anything about is presumed sellable; the flag next
                // to it says the presumption is all there is.
                condition?.Status ?? RoomConditionStatus.Clean,
                condition is not null,
                condition?.LastCleanedAt,
                condition?.LastCleanedBy,
                condition?.LastInspectedAt,
                condition?.LastInspectedBy,
                condition?.OutOfOrderReason,
                condition?.OutOfOrderUntil,
                occupancy,
                current?.Id,
                current?.CustomerCode,
                current?.GuestCount,
                task?.Id,
                task?.TaskType,
                task?.Status,
                task?.AssignedTo));
        }

        return ApplicationResult<RoomBoardResponse>.Success(new RoomBoardResponse(
            unit.UnitCode,
            date,
            rows.Count,
            rows.Count(row => row.ConditionStatus == RoomConditionStatus.Clean),
            rows.Count(row => row.ConditionStatus == RoomConditionStatus.Dirty),
            rows.Count(row => row.ConditionStatus == RoomConditionStatus.Inspected),
            rows.Count(row => row.ConditionStatus == RoomConditionStatus.OutOfOrder),
            rows.Count(row => row.OccupancyState == RoomOccupancyState.Departure),
            rows.Count(row => row.OccupancyState == RoomOccupancyState.Arrival),
            rows.Count(row => row.OccupancyState == RoomOccupancyState.Turnover),
            rows.Count(row => row.OccupancyState == RoomOccupancyState.Occupied),
            rows.Count(row => row.OccupancyState == RoomOccupancyState.Vacant),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Pending),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.InProgress),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Cleaned),
            rows));
    }

    public async Task<ApplicationResult<RoomConditionResponse>> SetRoomConditionAsync(
        Guid roomId,
        SetRoomConditionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == roomId, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomConditionResponse>.NotFound("Room was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var condition = await LoadOrCreateConditionAsync(room, cancellationToken);

        try
        {
            condition.Apply(
                request.Status,
                context.UserName,
                now,
                request.OutOfOrderReason,
                request.OutOfOrderUntil);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomConditionResponse>.Validation(ex.Message);
        }

        condition.MarkUpdated(context.UserName, now);

        await WriteAuditAsync(
            "housekeeping.room_condition.set",
            ConditionsEntity,
            condition.Id,
            context,
            new
            {
                condition.HotelUnitCode,
                condition.RoomId,
                room.Number,
                Status = condition.Status.ToString(),
                condition.OutOfOrderReason,
                condition.OutOfOrderUntil
            },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Two first-declarations on the same room raced past the exists-check and collided
            // on ux_room_conditions_room_id. Nothing was written; the caller reloads and retries.
            return ApplicationResult<RoomConditionResponse>.Conflict(
                "The condition of this room was just declared by a concurrent operation. Reload the board and try again.");
        }

        return ApplicationResult<RoomConditionResponse>.Success(Map(condition, room.Number));
    }

    // Tasks --------------------------------------------------------------------------------

    public async Task<IReadOnlyCollection<HousekeepingTaskResponse>> ListTasksAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        HousekeepingTaskStatus? status,
        string? assignedTo,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<HousekeepingTask>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(task => task.ServiceDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(task => task.ServiceDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(task => task.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(task => task.Status == status.Value);
        }

        var normalizedAttendant = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim();

        if (normalizedAttendant is not null)
        {
            query = query.Where(task => task.AssignedTo == normalizedAttendant);
        }

        var tasks = await query
            .OrderByDescending(task => task.ServiceDate)
            .ThenBy(task => task.RoomNumber)
            .ToArrayAsync(cancellationToken);

        return tasks.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> GetTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Set<HousekeepingTask>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        return task is null
            ? ApplicationResult<HousekeepingTaskResponse>.NotFound("Housekeeping task was not found.")
            : ApplicationResult<HousekeepingTaskResponse>.Success(Map(task));
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> CreateTaskAsync(
        CreateHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.RoomId, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<HousekeepingTaskResponse>.NotFound("Room was not found.");
        }

        if (!room.IsActive)
        {
            return ApplicationResult<HousekeepingTaskResponse>.Validation(
                "The room is inactive; no housekeeping task can be planned on it.");
        }

        HousekeepingTask task;
        var now = DateTimeOffset.UtcNow;

        try
        {
            task = new HousekeepingTask(
                room.HotelUnitCode,
                room.Id,
                room.Number,
                request.ServiceDate,
                request.TaskType,
                request.Notes);

            if (!string.IsNullOrWhiteSpace(request.AssignedTo))
            {
                task.AssignTo(request.AssignedTo, context.UserName, now);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<HousekeepingTaskResponse>.Validation(ex.Message);
        }

        task.MarkCreated(context.UserName, now);
        dbContext.Set<HousekeepingTask>().Add(task);

        await WriteAuditAsync(
            "housekeeping.task.created",
            TasksEntity,
            task.Id,
            context,
            new
            {
                task.HotelUnitCode,
                task.RoomNumber,
                task.ServiceDate,
                TaskType = task.TaskType.ToString(),
                task.AssignedTo
            },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<HousekeepingTaskResponse>.Conflict(
                "This room already carries a task of this type for this date.");
        }

        return ApplicationResult<HousekeepingTaskResponse>.Success(Map(task));
    }

    public async Task<ApplicationResult<GenerateHousekeepingTasksResponse>> GenerateDayTasksAsync(
        GenerateHousekeepingTasksRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unit = await RequireActiveHotelUnitAsync<GenerateHousekeepingTasksResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unit.Failure is not null)
        {
            return unit.Failure;
        }

        var serviceDate = request.ServiceDate;
        var nextDay = serviceDate.AddDays(1);

        var rooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.HotelUnitCode == unit.UnitCode && room.IsActive)
            .OrderBy(room => room.Number)
            .ToArrayAsync(cancellationToken);

        if (rooms.Length == 0)
        {
            return ApplicationResult<GenerateHousekeepingTasksResponse>.Validation(
                "This hotel unit has no active room; there is no sheet to generate.");
        }

        var roomIds = rooms.Select(room => room.Id).ToArray();

        var conditions = await dbContext.Set<RoomCondition>()
            .AsNoTracking()
            .Where(condition => roomIds.Contains(condition.RoomId))
            .ToDictionaryAsync(condition => condition.RoomId, cancellationToken);

        var stays = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.RoomId != null
                && roomIds.Contains(reservation.RoomId.Value)
                && reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate < nextDay
                && reservation.DepartureDate >= serviceDate)
            .ToArrayAsync(cancellationToken);

        // Le regroupement porte sur les sejours AFFECTES : depuis que le PMS vend par type, un
        // dossier peut n'avoir aucune chambre, et une feuille de menage ne s'ecrit que pour une
        // chambre reelle. Les dossiers sans chambre sont ecartes ici, ils reviendront quand la
        // reception aura affecte.
        var staysByRoom = stays
            .Where(reservation => reservation.RoomId is not null)
            .GroupBy(reservation => reservation.RoomId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var existing = await dbContext.Set<HousekeepingTask>()
            .AsNoTracking()
            .Where(task => task.HotelUnitCode == unit.UnitCode && task.ServiceDate == serviceDate)
            .Select(task => new { task.RoomId, task.TaskType })
            .ToArrayAsync(cancellationToken);

        // Idempotence key: (room, type). A cancelled task counts as existing on purpose - it was
        // withdrawn deliberately, and re-running the generation must not quietly put it back.
        var existingKeys = existing
            .Select(row => (row.RoomId, row.TaskType))
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var created = new List<HousekeepingTask>();
        var skippedExisting = 0;
        var skippedOutOfOrder = 0;

        foreach (var room in rooms)
        {
            var condition = conditions.GetValueOrDefault(room.Id);

            // A room withdrawn from service is not work to hand out: sending an attendant to a
            // room under repair is exactly what the withdrawal exists to prevent.
            if (condition?.Status == RoomConditionStatus.OutOfOrder)
            {
                skippedOutOfOrder++;
                continue;
            }

            var roomStays = staysByRoom.GetValueOrDefault(room.Id, []);
            var taskType = ChooseTaskType(roomStays, serviceDate, condition);

            if (taskType is not { } chosen)
            {
                continue;
            }

            if (!existingKeys.Add((room.Id, chosen)))
            {
                skippedExisting++;
                continue;
            }

            var task = new HousekeepingTask(room.HotelUnitCode, room.Id, room.Number, serviceDate, chosen);
            task.MarkCreated(context.UserName, now);

            dbContext.Set<HousekeepingTask>().Add(task);
            created.Add(task);
        }

        await WriteAuditAsync(
            "housekeeping.day_sheet.generated",
            TasksEntity,
            Guid.Empty,
            context,
            new
            {
                HotelUnitCode = unit.UnitCode,
                ServiceDate = serviceDate,
                Created = created.Count,
                SkippedExisting = skippedExisting,
                SkippedOutOfOrder = skippedOutOfOrder
            },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Two supervisors generated the same sheet at the same time: both passed the
            // exists-check, one lost on ux_housekeeping_tasks_room_date_type. Nothing was
            // written by this call, and the sheet the winner produced is already complete.
            return ApplicationResult<GenerateHousekeepingTasksResponse>.Conflict(
                "This day sheet was just generated by a concurrent operation. Reload it before generating again.");
        }

        return ApplicationResult<GenerateHousekeepingTasksResponse>.Success(
            new GenerateHousekeepingTasksResponse(
                unit.UnitCode,
                serviceDate,
                created.Count,
                skippedExisting,
                skippedOutOfOrder,
                created.Select(Map).ToArray()));
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> AssignTaskAsync(
        Guid id,
        AssignHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateTaskAsync(
            id,
            "housekeeping.task.assigned",
            (task, now) =>
            {
                task.AssignTo(request.AssignedTo, context.UserName, now);
                return new { task.AssignedTo };
            },
            context,
            cancellationToken);
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> StartTaskAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateTaskAsync(
            id,
            "housekeeping.task.started",
            (task, now) =>
            {
                task.Start(context.UserName, now);
                return new { task.AssignedTo };
            },
            context,
            cancellationToken);
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> CompleteTaskAsync(
        Guid id,
        CompleteHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Declaring the room done is also what makes it sellable again: the task and the room
        // condition move together, in one save, or not at all.
        return await MutateTaskAsync(
            id,
            "housekeeping.task.cleaned",
            (task, now) =>
            {
                task.MarkCleaned(context.UserName, now, request.Notes);
                return new { task.DurationMinutes };
            },
            context,
            cancellationToken,
            RoomConditionStatus.Clean);
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> InspectTaskAsync(
        Guid id,
        InspectHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateTaskAsync(
            id,
            request.Accepted ? "housekeeping.task.inspected" : "housekeeping.task.rejected",
            (task, now) =>
            {
                task.Inspect(request.Accepted, context.UserName, now, request.Notes);
                return new { request.Accepted, task.InspectionNotes };
            },
            context,
            cancellationToken,
            // Accepting a room states it is checked; refusing it states it is dirty again. The
            // board must say the same thing as the sheet the moment the supervisor decides.
            request.Accepted ? RoomConditionStatus.Inspected : RoomConditionStatus.Dirty);
    }

    public async Task<ApplicationResult<HousekeepingTaskResponse>> CancelTaskAsync(
        Guid id,
        CancelHousekeepingTaskRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // No condition change: withdrawing a task says nothing about the state of the room. A
        // room whose clean was cancelled is exactly as dirty as it was before.
        return await MutateTaskAsync(
            id,
            "housekeeping.task.cancelled",
            (task, now) =>
            {
                task.Cancel(request.Reason, context.UserName, now);
                return new { task.CancelReason };
            },
            context,
            cancellationToken);
    }

    public async Task<ApplicationResult<HousekeepingDaySheetResponse>> GetDaySheetAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var unit = await RequireActiveHotelUnitAsync<HousekeepingDaySheetResponse>(hotelUnitCode, cancellationToken);

        if (unit.Failure is not null)
        {
            return unit.Failure;
        }

        // Cancelled tasks are excluded everywhere on this screen: they are no longer work to
        // distribute, and counting them would overstate every attendant load.
        var tasks = await dbContext.Set<HousekeepingTask>()
            .AsNoTracking()
            .Where(task => task.HotelUnitCode == unit.UnitCode
                && task.ServiceDate == date
                && task.Status != HousekeepingTaskStatus.Cancelled)
            .OrderBy(task => task.RoomNumber)
            .ToArrayAsync(cancellationToken);

        var attendants = tasks
            .Where(task => task.AssignedTo is not null)
            .GroupBy(task => task.AssignedTo!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new HousekeepingAttendantLoad(
                group.First().AssignedTo!,
                group.Count(),
                group.Count(task => task.Status == HousekeepingTaskStatus.Pending),
                group.Count(task => task.Status == HousekeepingTaskStatus.InProgress),
                group.Count(task => task.Status == HousekeepingTaskStatus.Cleaned),
                group.Count(task => task.Status == HousekeepingTaskStatus.Inspected),
                group.Count(task => task.Status == HousekeepingTaskStatus.Rejected),
                group.Sum(task => task.DurationMinutes ?? 0)))
            .OrderByDescending(load => load.TaskCount)
            .ThenBy(load => load.AssignedTo, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return ApplicationResult<HousekeepingDaySheetResponse>.Success(new HousekeepingDaySheetResponse(
            unit.UnitCode,
            date,
            tasks.Length,
            tasks.Count(task => task.AssignedTo is null),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Pending),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.InProgress),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Cleaned),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Inspected),
            tasks.Count(task => task.Status == HousekeepingTaskStatus.Rejected),
            attendants,
            tasks.Select(Map).ToArray()));
    }

    // Minibar ------------------------------------------------------------------------------

    public async Task<IReadOnlyCollection<MinibarItemResponse>> ListMinibarItemsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<MinibarItem>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(item => item.HotelUnitCode == normalizedUnitCode);
        }

        var items = await query
            .OrderBy(item => item.HotelUnitCode)
            .ThenBy(item => item.Code)
            .ToArrayAsync(cancellationToken);

        return items.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<MinibarItemResponse>> CreateMinibarItemAsync(
        CreateMinibarItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unit = await RequireActiveHotelUnitAsync<MinibarItemResponse>(request.HotelUnitCode, cancellationToken);

        if (unit.Failure is not null)
        {
            return unit.Failure;
        }

        MinibarItem item;

        try
        {
            item = new MinibarItem(unit.UnitCode, request.Code, request.Label, request.UnitPrice);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<MinibarItemResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<MinibarItem>()
            .AnyAsync(
                current => current.HotelUnitCode == item.HotelUnitCode && current.Code == item.Code,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<MinibarItemResponse>.Conflict(
                "A minibar item with this code already exists in this hotel unit.");
        }

        item.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<MinibarItem>().Add(item);

        await WriteAuditAsync(
            "housekeeping.minibar_item.created",
            MinibarItemsEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.Label, item.UnitPrice },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<MinibarItemResponse>.Conflict(
                "A minibar item with this code already exists in this hotel unit.");
        }

        return ApplicationResult<MinibarItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<MinibarItemResponse>> UpdateMinibarItemAsync(
        Guid id,
        UpdateMinibarItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<MinibarItem>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<MinibarItemResponse>.NotFound("Minibar item was not found.");
        }

        try
        {
            item.UpdateDetails(request.Label, request.UnitPrice);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<MinibarItemResponse>.Validation(ex.Message);
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        // The new price applies to what is recorded from now on. Consumptions already recorded
        // keep the price frozen into them - a price list edited on Friday must not rewrite what
        // a guest was charged on Monday.
        await WriteAuditAsync(
            "housekeeping.minibar_item.updated",
            MinibarItemsEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.Label, item.UnitPrice },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<MinibarItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<MinibarItemResponse>> SetMinibarItemActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<MinibarItem>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<MinibarItemResponse>.NotFound("Minibar item was not found.");
        }

        if (isActive)
        {
            item.Activate();
        }
        else
        {
            item.Deactivate();
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "housekeeping.minibar_item.activated" : "housekeeping.minibar_item.deactivated",
            MinibarItemsEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.IsActive },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<MinibarItemResponse>.Success(Map(item));
    }

    public async Task<IReadOnlyCollection<MinibarConsumptionResponse>> ListMinibarConsumptionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        Guid? reservationId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<MinibarConsumption>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(consumption => consumption.ConsumedOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(consumption => consumption.ConsumedOn <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(consumption => consumption.HotelUnitCode == normalizedUnitCode);
        }

        if (reservationId.HasValue)
        {
            query = query.Where(consumption => consumption.ReservationId == reservationId.Value);
        }

        var consumptions = await query
            .OrderByDescending(consumption => consumption.ConsumedOn)
            .ThenBy(consumption => consumption.RoomNumber)
            .ToArrayAsync(cancellationToken);

        return consumptions.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<MinibarConsumptionResponse>> RecordMinibarConsumptionAsync(
        RecordMinibarConsumptionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.ReservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<MinibarConsumptionResponse>.NotFound("Reservation was not found.");
        }

        // Checked early for a readable refusal. The authoritative guard is the one inside
        // AddFolioChargeAsync, which re-reads the status inside its own Serializable transaction
        // and loses cleanly against a check-out committed in between.
        if (reservation.Status != ReservationStatus.CheckedIn)
        {
            return ApplicationResult<MinibarConsumptionResponse>.Conflict(
                "Minibar consumption can only be recorded while the guest is checked in.");
        }

        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == reservation.RoomId, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<MinibarConsumptionResponse>.NotFound(
                "The room of this reservation was not found.");
        }

        string normalizedItemCode;

        try
        {
            normalizedItemCode = MinibarItem.NormalizeCode(request.ItemCode);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<MinibarConsumptionResponse>.Validation(ex.Message);
        }

        var item = await dbContext.Set<MinibarItem>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == reservation.HotelUnitCode && current.Code == normalizedItemCode,
                cancellationToken);

        if (item is null)
        {
            return ApplicationResult<MinibarConsumptionResponse>.NotFound(
                "No minibar item with this code exists in the hotel unit of this reservation.");
        }

        if (!item.IsActive)
        {
            return ApplicationResult<MinibarConsumptionResponse>.Validation(
                "This minibar item is inactive; it can no longer be charged.");
        }

        MinibarConsumption consumption;

        try
        {
            consumption = new MinibarConsumption(
                reservation.HotelUnitCode,
                room.Id,
                room.Number,
                reservation.Id,
                item.Code,
                item.Label,
                item.UnitPrice,
                request.Quantity,
                request.ConsumedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<MinibarConsumptionResponse>.Validation(ex.Message);
        }

        consumption.MarkCreated(context.UserName, DateTimeOffset.UtcNow);

        // Tracked but NOT saved: AddFolioChargeAsync flushes inside its own Serializable
        // transaction, so this row and the folio line it pays for are committed together.
        var entry = dbContext.Set<MinibarConsumption>().Add(consumption);

        var folioResult = await lodgingService.AddFolioChargeAsync(
            reservation.Id,
            new AddFolioChargeRequest(
                consumption.ConsumedOn,
                BuildFolioLabel(consumption),
                consumption.TotalAmount,
                ChargeKind.Extra,
                consumption.Id.ToString()),
            context,
            cancellationToken);

        if (!folioResult.Succeeded)
        {
            // Nothing was charged, so nothing must be recorded either. Detaching keeps the
            // pending row out of any later SaveChanges on this scoped context.
            entry.State = EntityState.Detached;

            return folioResult.ErrorType switch
            {
                ApplicationErrorType.NotFound =>
                    ApplicationResult<MinibarConsumptionResponse>.NotFound(folioResult.Error ?? "Folio was not found."),
                ApplicationErrorType.Validation =>
                    ApplicationResult<MinibarConsumptionResponse>.Validation(folioResult.Error ?? "The consumption could not be billed."),
                _ => ApplicationResult<MinibarConsumptionResponse>.Conflict(
                    folioResult.Error ?? "The consumption could not be billed on the folio."),
            };
        }

        await WriteAuditAsync(
            "housekeeping.minibar_consumption.recorded",
            MinibarConsumptionsEntity,
            consumption.Id,
            context,
            new
            {
                consumption.HotelUnitCode,
                consumption.RoomNumber,
                consumption.ReservationId,
                consumption.ItemCode,
                consumption.Quantity,
                consumption.TotalAmount
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<MinibarConsumptionResponse>.Success(Map(consumption));
    }

    // Internals ------------------------------------------------------------------------------

    /// <summary>
    /// What the reservations say about a room on one day. The room is a Turnover when a stay
    /// ends AND another starts, a Departure when only one ends, an Arrival when only one starts,
    /// Occupied when a checked-in stay covers the night without ending, Vacant otherwise.
    /// A stay whose departure day is the queried day no longer OCCUPIES it - the night belongs
    /// to whoever arrives - which is why departures and arrivals are read separately.
    /// </summary>
    private static RoomOccupancyState ClassifyOccupancy(IReadOnlyCollection<Reservation> stays, DateOnly date)
    {
        var departing = stays.Any(stay => stay.DepartureDate == date);
        var arriving = stays.Any(stay => stay.ArrivalDate == date);

        if (departing && arriving)
        {
            return RoomOccupancyState.Turnover;
        }

        if (departing)
        {
            return RoomOccupancyState.Departure;
        }

        if (arriving)
        {
            return RoomOccupancyState.Arrival;
        }

        var occupied = stays.Any(stay => stay.Status == ReservationStatus.CheckedIn
            && stay.ArrivalDate <= date
            && stay.DepartureDate > date);

        return occupied ? RoomOccupancyState.Occupied : RoomOccupancyState.Vacant;
    }

    /// <summary>
    /// The stay the board shows next to the room. The arriving guest wins over the leaving one:
    /// on a turnover, the name that matters to the floor is the one the room must be ready for.
    /// </summary>
    private static Reservation? PickRepresentativeStay(IReadOnlyCollection<Reservation> stays, DateOnly date)
    {
        return stays.FirstOrDefault(stay => stay.ArrivalDate == date)
            ?? stays.FirstOrDefault(stay => stay.ArrivalDate <= date && stay.DepartureDate > date)
            ?? stays.FirstOrDefault(stay => stay.DepartureDate == date);
    }

    /// <summary>
    /// The service a room needs on the generated day, or null when it needs none: a departure
    /// clean when a stay ends, a stayover service when the guest is in and stays, a refresh when
    /// the room is free but was left dirty. A free room already clean or inspected is not work.
    /// </summary>
    private static HousekeepingTaskType? ChooseTaskType(
        IReadOnlyCollection<Reservation> stays,
        DateOnly date,
        RoomCondition? condition)
    {
        if (stays.Any(stay => stay.DepartureDate == date))
        {
            return HousekeepingTaskType.Departure;
        }

        var occupied = stays.Any(stay => stay.Status == ReservationStatus.CheckedIn
            && stay.ArrivalDate <= date
            && stay.DepartureDate > date);

        if (occupied)
        {
            return HousekeepingTaskType.Stayover;
        }

        return condition?.Status == RoomConditionStatus.Dirty
            ? HousekeepingTaskType.Vacant
            : null;
    }

    /// <summary>
    /// Loads a task, applies one transition, optionally drives the room condition with it, and
    /// saves the two together. The transition itself lives in the entity: everything here is
    /// about the rows AROUND the task, which the entity cannot see.
    ///
    /// The <see cref="AuditableEntity.UpdatedAt"/> stamp read before the mutation is used as an
    /// optimistic guard: two supervisors deciding on the same room at once must not have the
    /// second silently overwrite the first.
    /// </summary>
    private async Task<ApplicationResult<HousekeepingTaskResponse>> MutateTaskAsync(
        Guid id,
        string auditAction,
        Func<HousekeepingTask, DateTimeOffset, object> mutate,
        OperationContext context,
        CancellationToken cancellationToken,
        RoomConditionStatus? conditionToApply = null)
    {
        var task = await dbContext.Set<HousekeepingTask>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (task is null)
        {
            return ApplicationResult<HousekeepingTaskResponse>.NotFound("Housekeeping task was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        object details;

        try
        {
            details = mutate(task, now);
        }
        catch (InvalidOperationException ex)
        {
            // The entity refused the transition: the caller is acting on a state the sheet no
            // longer shows. That is a conflict, not a bad request.
            return ApplicationResult<HousekeepingTaskResponse>.Conflict(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<HousekeepingTaskResponse>.Validation(ex.Message);
        }

        task.MarkUpdated(context.UserName, now);

        if (conditionToApply is { } status)
        {
            var room = await dbContext.Set<Room>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Id == task.RoomId, cancellationToken);

            if (room is not null)
            {
                var condition = await LoadOrCreateConditionAsync(room, cancellationToken);
                condition.Apply(status, context.UserName, now);
                condition.MarkUpdated(context.UserName, now);
            }
        }

        await WriteAuditAsync(
            auditAction,
            TasksEntity,
            task.Id,
            context,
            new
            {
                task.HotelUnitCode,
                task.RoomNumber,
                task.ServiceDate,
                Status = task.Status.ToString(),
                Details = details
            },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<HousekeepingTaskResponse>.Conflict(ConcurrentTaskMutationRefused);
        }

        return ApplicationResult<HousekeepingTaskResponse>.Success(Map(task));
    }

    /// <summary>
    /// The tracked condition row of a room, created (but not saved) when the room has none. The
    /// unique index on room_id is what makes the race harmless: two first-declarations collide
    /// there rather than producing two current states for one room.
    /// </summary>
    private async Task<RoomCondition> LoadOrCreateConditionAsync(Room room, CancellationToken cancellationToken)
    {
        var condition = await dbContext.Set<RoomCondition>()
            .SingleOrDefaultAsync(current => current.RoomId == room.Id, cancellationToken);

        if (condition is not null)
        {
            return condition;
        }

        condition = new RoomCondition(room.HotelUnitCode, room.Id);
        condition.MarkCreated("system", DateTimeOffset.UtcNow);
        dbContext.Set<RoomCondition>().Add(condition);

        return condition;
    }

    private async Task<(string UnitCode, ApplicationResult<T>? Failure)> RequireActiveHotelUnitAsync<T>(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        string normalized;

        try
        {
            normalized = HotelUnit.NormalizeCode(hotelUnitCode);
        }
        catch (ArgumentException ex)
        {
            return (string.Empty, ApplicationResult<T>.Validation(ex.Message));
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalized, cancellationToken);

        if (unit is null)
        {
            return (normalized, ApplicationResult<T>.NotFound("Hotel unit was not found."));
        }

        if (!unit.IsActive)
        {
            return (normalized, ApplicationResult<T>.Validation("The hotel unit is inactive."));
        }

        return (normalized, null);
    }

    /// <summary>
    /// What the guest reads on their bill. The machine link back to the housekeeping record is
    /// the folio line reference (the consumption id), not this text.
    /// </summary>
    private static string BuildFolioLabel(MinibarConsumption consumption)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "Minibar {0} x{1} - chambre {2}",
            consumption.ItemLabel,
            consumption.Quantity,
            consumption.RoomNumber);
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private static HousekeepingTaskResponse Map(HousekeepingTask task)
    {
        return new HousekeepingTaskResponse(
            task.Id,
            task.HotelUnitCode,
            task.RoomId,
            task.RoomNumber,
            task.ServiceDate,
            task.TaskType,
            task.Status,
            task.AssignedTo,
            task.AssignedAt,
            task.AssignedBy,
            task.StartedAt,
            task.StartedBy,
            task.CleanedAt,
            task.CleanedBy,
            task.DurationMinutes,
            task.InspectedAt,
            task.InspectedBy,
            task.InspectionNotes,
            task.CancelledAt,
            task.CancelledBy,
            task.CancelReason,
            task.Notes,
            task.CreatedAt,
            task.CreatedBy,
            task.UpdatedAt,
            task.UpdatedBy);
    }

    private static RoomConditionResponse Map(RoomCondition condition, string roomNumber)
    {
        return new RoomConditionResponse(
            condition.RoomId,
            condition.HotelUnitCode,
            roomNumber,
            condition.Status,
            condition.LastCleanedAt,
            condition.LastCleanedBy,
            condition.LastInspectedAt,
            condition.LastInspectedBy,
            condition.OutOfOrderReason,
            condition.OutOfOrderUntil,
            condition.UpdatedAt,
            condition.UpdatedBy);
    }

    private static MinibarItemResponse Map(MinibarItem item)
    {
        return new MinibarItemResponse(
            item.Id,
            item.HotelUnitCode,
            item.Code,
            item.Label,
            item.UnitPrice,
            item.IsActive,
            item.CreatedAt,
            item.CreatedBy,
            item.UpdatedAt,
            item.UpdatedBy);
    }

    private static MinibarConsumptionResponse Map(MinibarConsumption consumption)
    {
        return new MinibarConsumptionResponse(
            consumption.Id,
            consumption.HotelUnitCode,
            consumption.RoomId,
            consumption.RoomNumber,
            consumption.ReservationId,
            consumption.ItemCode,
            consumption.ItemLabel,
            consumption.UnitPrice,
            consumption.Quantity,
            consumption.TotalAmount,
            consumption.ConsumedOn,
            consumption.Notes,
            consumption.CreatedAt,
            consumption.CreatedBy);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
