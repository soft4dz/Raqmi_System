namespace RaqmiSystem.Domain.Receivables;

/// <summary>
/// Places an outstanding invoice in its age bracket.
///
/// IMPORTANT - the age is counted from the INVOICE DATE, not from a due date: the Invoice
/// aggregate (RaqmiSystem.Domain.Billing.Invoice) carries no due date and the system holds no
/// payment terms, so there is no honest way to derive one. Rather than hard-coding an implicit
/// "30 days net" that nobody agreed to, the whole aging report is expressed relative to the
/// invoice date and says so in its own payload (see AgingBalanceResponse.AgingBasis). The day
/// a due date is introduced on Invoice, only this class and that sentence have to change.
///
/// Consequence of that choice on the <see cref="AgingBucket.NotDue"/> bracket: without a due
/// date, an invoice is considered "not due" only while its own date has not been passed, i.e.
/// an age of zero days or less (an invoice dated on - or after - the reporting date).
/// </summary>
public static class AgingCalculator
{
    /// <summary>
    /// Number of days between the invoice date and the reporting date. Negative when the
    /// invoice is dated after the reporting date.
    /// </summary>
    public static int AgeInDays(DateOnly invoiceDate, DateOnly asOfDate)
    {
        return asOfDate.DayNumber - invoiceDate.DayNumber;
    }

    public static AgingBucket Classify(DateOnly invoiceDate, DateOnly asOfDate)
    {
        return ClassifyAge(AgeInDays(invoiceDate, asOfDate));
    }

    public static AgingBucket ClassifyAge(int ageInDays)
    {
        return ageInDays switch
        {
            <= 0 => AgingBucket.NotDue,
            <= 30 => AgingBucket.Days1To30,
            <= 60 => AgingBucket.Days31To60,
            <= 90 => AgingBucket.Days61To90,
            _ => AgingBucket.Over90
        };
    }
}
