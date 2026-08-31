using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Tests;

/// <summary>
/// Domain coverage of the housekeeping module: the task lifecycle, the room condition and the
/// minibar snapshot rules. These are the invariants an entity can guarantee ALONE - everything
/// that needs to see other rows (the day-sheet generation, the folio the minibar bills) is
/// covered over HTTP by <see cref="HousekeepingEndpointTests"/>.
/// </summary>
public sealed class HousekeepingTests
{
    private static readonly DateOnly ServiceDate = new(2026, 8, 31);

    private static readonly DateTimeOffset Now = new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_task_is_pending_unassigned_and_normalizes_its_room_number()
    {
        var task = NewTask();

        Assert.Equal(HousekeepingTaskStatus.Pending, task.Status);
        Assert.Null(task.AssignedTo);
        Assert.False(task.IsClosed);

        // The room number is normalized (trimmed, upper-cased) exactly like Room.Number, so the
        // snapshot on the sheet always matches the room it was taken from.
        Assert.Equal("101B", task.RoomNumber);
        Assert.Equal("HTL", task.HotelUnitCode);
    }

    [Fact]
    public void Task_cannot_be_started_before_it_is_assigned()
    {
        var task = NewTask();

        // The point of the planning: nobody is in the room until somebody is responsible for it.
        var failure = Assert.Throws<InvalidOperationException>(() => task.Start("supervisor", Now));
        Assert.Contains("assigned", failure.Message, StringComparison.OrdinalIgnoreCase);

        task.AssignTo("Amina", "supervisor", Now);
        task.Start("Amina", Now);

        Assert.Equal(HousekeepingTaskStatus.InProgress, task.Status);
        Assert.Equal("Amina", task.AssignedTo);
        Assert.Equal(Now, task.StartedAt);
    }

    [Fact]
    public void Full_cycle_runs_from_assignment_to_accepted_inspection()
    {
        var task = NewTask();

        task.AssignTo("Amina", "supervisor", Now);
        task.Start("Amina", Now);
        task.MarkCleaned("Amina", Now.AddMinutes(25));

        Assert.Equal(HousekeepingTaskStatus.Cleaned, task.Status);
        Assert.Equal(25, task.DurationMinutes);

        task.Inspect(accepted: true, "supervisor", Now.AddMinutes(40), "Rien a signaler");

        Assert.Equal(HousekeepingTaskStatus.Inspected, task.Status);
        Assert.True(task.IsClosed);
        Assert.Equal("supervisor", task.InspectedBy);
        Assert.Equal("Rien a signaler", task.InspectionNotes);
    }

    [Fact]
    public void Refused_inspection_demands_a_reason_and_sends_the_task_back_to_work()
    {
        var task = CleanedTask();

        // A refusal nobody can explain teaches the attendant nothing: the entity refuses it.
        Assert.Throws<ArgumentException>(() => task.Inspect(accepted: false, "supervisor", Now, notes: null));
        Assert.Equal(HousekeepingTaskStatus.Cleaned, task.Status);

        task.Inspect(accepted: false, "supervisor", Now.AddMinutes(45), "Salle de bain non faite");

        Assert.Equal(HousekeepingTaskStatus.Rejected, task.Status);
        Assert.False(task.IsClosed);
        Assert.Equal("Salle de bain non faite", task.InspectionNotes);

        // A rejected task is startable again - that is the whole point of sending it back.
        task.Start("Amina", Now.AddMinutes(50));
        Assert.Equal(HousekeepingTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Second_pass_reports_its_own_duration_not_the_refused_one()
    {
        var task = CleanedTask();

        Assert.Equal(25, task.DurationMinutes);

        task.Inspect(accepted: false, "supervisor", Now.AddMinutes(30), "A refaire");

        // Restarting clears the verdict AND the duration: what the sheet must report is the pass
        // that ends up being accepted, not the attempt that was refused.
        task.Start("Amina", Now.AddMinutes(35));
        Assert.Null(task.DurationMinutes);
        Assert.Null(task.InspectedAt);

        task.MarkCleaned("Amina", Now.AddMinutes(45));
        Assert.Equal(10, task.DurationMinutes);
    }

    [Fact]
    public void Only_a_cleaned_task_can_be_inspected()
    {
        var task = NewTask();
        task.AssignTo("Amina", "supervisor", Now);

        Assert.Throws<InvalidOperationException>(() => task.Inspect(accepted: true, "supervisor", Now));

        task.Start("Amina", Now);
        Assert.Throws<InvalidOperationException>(() => task.Inspect(accepted: true, "supervisor", Now));
    }

    [Fact]
    public void Closed_task_refuses_every_further_transition()
    {
        var task = CleanedTask();
        task.Inspect(accepted: true, "supervisor", Now.AddMinutes(30));

        Assert.Throws<InvalidOperationException>(() => task.AssignTo("Karim", "supervisor", Now));
        Assert.Throws<InvalidOperationException>(() => task.Cancel("plus besoin", "supervisor", Now));
        Assert.Throws<InvalidOperationException>(() => task.Start("Amina", Now));
    }

    [Fact]
    public void Cancelling_demands_a_reason_and_closes_the_task()
    {
        var task = NewTask();

        Assert.Throws<ArgumentException>(() => task.Cancel("   ", "supervisor", Now));

        task.Cancel("Chambre bloquee par la technique", "supervisor", Now);

        Assert.Equal(HousekeepingTaskStatus.Cancelled, task.Status);
        Assert.True(task.IsClosed);
        Assert.Equal("Chambre bloquee par la technique", task.CancelReason);
    }

    [Fact]
    public void Room_condition_starts_clean_and_stamps_the_trail_it_reaches()
    {
        var condition = new RoomCondition("htl", Guid.NewGuid());

        Assert.Equal(RoomConditionStatus.Clean, condition.Status);
        Assert.Null(condition.LastCleanedAt);

        condition.Apply(RoomConditionStatus.Clean, "Amina", Now);
        Assert.Equal(Now, condition.LastCleanedAt);
        Assert.Equal("Amina", condition.LastCleanedBy);
        Assert.Null(condition.LastInspectedAt);

        // Inspected implies cleaned: a supervisor who signs a room off states it is serviced,
        // not only checked, so both trails move.
        condition.Apply(RoomConditionStatus.Inspected, "supervisor", Now.AddMinutes(10));
        Assert.Equal(Now.AddMinutes(10), condition.LastCleanedAt);
        Assert.Equal(Now.AddMinutes(10), condition.LastInspectedAt);
        Assert.Equal("supervisor", condition.LastInspectedBy);
    }

    [Fact]
    public void Out_of_order_demands_a_reason_and_every_other_status_clears_it()
    {
        var condition = new RoomCondition("HTL", Guid.NewGuid());

        Assert.Throws<ArgumentException>(
            () => condition.Apply(RoomConditionStatus.OutOfOrder, "supervisor", Now));

        condition.Apply(
            RoomConditionStatus.OutOfOrder,
            "supervisor",
            Now,
            "Fuite salle de bain",
            new DateOnly(2026, 9, 5));

        Assert.Equal(RoomConditionStatus.OutOfOrder, condition.Status);
        Assert.Equal("Fuite salle de bain", condition.OutOfOrderReason);
        Assert.Equal(new DateOnly(2026, 9, 5), condition.OutOfOrderUntil);

        condition.Apply(RoomConditionStatus.Dirty, "supervisor", Now.AddDays(1));

        Assert.Null(condition.OutOfOrderReason);
        Assert.Null(condition.OutOfOrderUntil);
    }

    [Fact]
    public void Minibar_item_price_must_be_strictly_positive_with_at_most_two_decimals()
    {
        // Zero is refused at the source, because a folio line may never carry a zero amount: a
        // complimentary item is a priced product plus a gesture, not a free product.
        Assert.Throws<ArgumentOutOfRangeException>(() => new MinibarItem("HTL", "EAU50", "Eau 50cl", 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MinibarItem("HTL", "EAU50", "Eau 50cl", -1m));
        Assert.Throws<ArgumentException>(() => new MinibarItem("HTL", "EAU50", "Eau 50cl", 120.005m));

        var item = new MinibarItem("htl", " eau50 ", "Eau minerale 50 cl", 120.50m);

        Assert.Equal("HTL", item.HotelUnitCode);
        Assert.Equal("EAU50", item.Code);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Minibar_consumption_freezes_the_price_list_and_computes_its_own_total()
    {
        var consumption = new MinibarConsumption(
            "htl",
            Guid.NewGuid(),
            "101",
            Guid.NewGuid(),
            "eau50",
            "Eau minerale 50 cl",
            120.50m,
            3,
            ServiceDate);

        Assert.Equal("EAU50", consumption.ItemCode);
        Assert.Equal(361.50m, consumption.TotalAmount);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MinibarConsumption(
            "HTL",
            Guid.NewGuid(),
            "101",
            Guid.NewGuid(),
            "EAU50",
            "Eau minerale 50 cl",
            120.50m,
            0,
            ServiceDate));
    }

    private static HousekeepingTask NewTask()
    {
        return new HousekeepingTask("htl", Guid.NewGuid(), " 101b ", ServiceDate, HousekeepingTaskType.Departure);
    }

    private static HousekeepingTask CleanedTask()
    {
        var task = NewTask();
        task.AssignTo("Amina", "supervisor", Now);
        task.Start("Amina", Now);
        task.MarkCleaned("Amina", Now.AddMinutes(25));
        return task;
    }
}
