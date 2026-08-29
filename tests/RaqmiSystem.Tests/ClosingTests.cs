using RaqmiSystem.Domain.Closing;

namespace RaqmiSystem.Tests;

public sealed class ClosingTests
{
    [Fact]
    public void Constructor_closes_the_day_and_normalizes_unit_code()
    {
        var closedAt = DateTimeOffset.UtcNow;

        var closing = new DailyClosing(
            new DateOnly(2026, 2, 1),
            " el-manar ",
            "night.auditor",
            closedAt,
            " Journee complete ");

        Assert.Equal("EL-MANAR", closing.HotelUnitCode);
        Assert.Equal(ClosingStatus.Closed, closing.Status);
        Assert.True(closing.IsClosed);
        Assert.Equal("night.auditor", closing.ClosedBy);
        Assert.Equal(closedAt, closing.ClosedAt);
        Assert.Equal("Journee complete", closing.Notes);
        Assert.Null(closing.ReopenedAt);
        Assert.Null(closing.ReopenedBy);
        Assert.Null(closing.ReopenReason);
    }

    [Fact]
    public void Reopen_without_a_reason_is_rejected()
    {
        var closing = CreateClosed();

        Assert.Throws<ArgumentException>(() =>
            closing.Reopen("   ", "controller", DateTimeOffset.UtcNow));

        Assert.Equal(ClosingStatus.Closed, closing.Status);
    }

    [Fact]
    public void Reopen_records_reason_and_actor()
    {
        var closing = CreateClosed();
        var reopenedAt = DateTimeOffset.UtcNow;

        closing.Reopen(" Ecart de caisse a corriger ", "controller", reopenedAt);

        Assert.Equal(ClosingStatus.Reopened, closing.Status);
        Assert.False(closing.IsClosed);
        Assert.Equal("Ecart de caisse a corriger", closing.ReopenReason);
        Assert.Equal("controller", closing.ReopenedBy);
        Assert.Equal(reopenedAt, closing.ReopenedAt);
    }

    [Fact]
    public void Reopen_requires_a_closed_status()
    {
        var closing = CreateClosed();
        closing.Reopen("First reopening.", "controller", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            closing.Reopen("Second reopening.", "controller", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reopened_day_can_be_closed_again_and_keeps_the_reopening_trail()
    {
        var closing = CreateClosed();
        closing.Reopen("Missing folio.", "controller", DateTimeOffset.UtcNow);

        var reclosedAt = DateTimeOffset.UtcNow;
        closing.CloseAgain("night.auditor.2", reclosedAt);

        Assert.Equal(ClosingStatus.Closed, closing.Status);
        Assert.True(closing.IsClosed);
        Assert.Equal("night.auditor.2", closing.ClosedBy);
        Assert.Equal(reclosedAt, closing.ClosedAt);

        // The last reopening cycle stays visible for audit purposes.
        Assert.Equal("Missing folio.", closing.ReopenReason);
        Assert.Equal("controller", closing.ReopenedBy);
        Assert.NotNull(closing.ReopenedAt);
    }

    [Fact]
    public void CloseAgain_requires_a_reopened_status()
    {
        var closing = CreateClosed();

        Assert.Throws<InvalidOperationException>(() =>
            closing.CloseAgain("night.auditor", DateTimeOffset.UtcNow));
    }

    private static DailyClosing CreateClosed()
    {
        return new DailyClosing(
            new DateOnly(2026, 2, 1),
            "EL-MANAR",
            "night.auditor",
            DateTimeOffset.UtcNow);
    }
}
