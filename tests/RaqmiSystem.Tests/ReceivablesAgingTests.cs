using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the receivables module: the age brackets and the invariants the
/// <see cref="Reminder"/> entity enforces on its own (no database involved).
///
/// The bracket boundaries are the whole point of an aged balance, so they are pinned to the day:
/// an invoice that slides one bucket to the right changes what a credit manager decides.
/// </summary>
public sealed class ReceivablesAgingTests
{
    /// <summary>Every case below is aged against this reporting date.</summary>
    private const int AsOfYear = 2026;
    private const int AsOfMonth = 6;
    private const int AsOfDay = 30;

    [Theory]
    // Dated after the reporting date, and dated on it: nothing is overdue yet. Without a due
    // date on the invoice, "not due" can only mean "its own date has not been passed".
    [InlineData(2026, 7, 15, AgingBucket.NotDue)]
    [InlineData(2026, 6, 30, AgingBucket.NotDue)]
    // 1 day and 30 days: both ends of the first bracket.
    [InlineData(2026, 6, 29, AgingBucket.Days1To30)]
    [InlineData(2026, 5, 31, AgingBucket.Days1To30)]
    // 31 days and 60 days.
    [InlineData(2026, 5, 30, AgingBucket.Days31To60)]
    [InlineData(2026, 5, 1, AgingBucket.Days31To60)]
    // 61 days and 90 days.
    [InlineData(2026, 4, 30, AgingBucket.Days61To90)]
    [InlineData(2026, 4, 1, AgingBucket.Days61To90)]
    // 91 days: the first day beyond the last bracket.
    [InlineData(2026, 3, 31, AgingBucket.Over90)]
    [InlineData(2025, 12, 31, AgingBucket.Over90)]
    public void Classify_places_an_invoice_in_the_bracket_of_its_exact_age(
        int year,
        int month,
        int day,
        AgingBucket expected)
    {
        var asOfDate = new DateOnly(AsOfYear, AsOfMonth, AsOfDay);

        Assert.Equal(expected, AgingCalculator.Classify(new DateOnly(year, month, day), asOfDate));
    }

    [Fact]
    public void AgeInDays_counts_from_the_invoice_date_and_goes_negative_for_a_future_invoice()
    {
        var asOfDate = new DateOnly(2026, 6, 30);

        Assert.Equal(0, AgingCalculator.AgeInDays(new DateOnly(2026, 6, 30), asOfDate));
        Assert.Equal(91, AgingCalculator.AgeInDays(new DateOnly(2026, 3, 31), asOfDate));
        Assert.Equal(-15, AgingCalculator.AgeInDays(new DateOnly(2026, 7, 15), asOfDate));
    }

    [Fact]
    public void Reminder_normalizes_the_customer_code_and_the_invoice_number()
    {
        var reminder = new Reminder(
            "  cli-a  ",
            "  fac-2026-000001 ",
            ReminderLevel.Second,
            new DateOnly(2026, 6, 1),
            ReminderChannel.Email,
            "  Relance par courriel  ");

        Assert.Equal("CLI-A", reminder.CustomerCode);
        Assert.Equal("FAC-2026-000001", reminder.InvoiceNumber);
        Assert.Equal("Relance par courriel", reminder.Notes);
    }

    [Fact]
    public void Reminder_requires_an_invoice_number()
    {
        Assert.Throws<ArgumentException>(() => new Reminder(
            "CLI-A",
            "   ",
            ReminderLevel.First,
            new DateOnly(2026, 6, 1),
            ReminderChannel.Phone));
    }

    [Fact]
    public void Reminder_refuses_an_undefined_level_or_channel()
    {
        Assert.Throws<ArgumentException>(() => new Reminder(
            "CLI-A",
            "FAC-2026-000001",
            (ReminderLevel)42,
            new DateOnly(2026, 6, 1),
            ReminderChannel.Phone));

        Assert.Throws<ArgumentException>(() => new Reminder(
            "CLI-A",
            "FAC-2026-000001",
            ReminderLevel.First,
            new DateOnly(2026, 6, 1),
            (ReminderChannel)42));
    }

    [Fact]
    public void Reminder_levels_are_ordered_from_the_courtesy_reminder_to_the_formal_notice()
    {
        Assert.True(ReminderLevel.First < ReminderLevel.Second);
        Assert.True(ReminderLevel.Second < ReminderLevel.FormalNotice);
    }
}
