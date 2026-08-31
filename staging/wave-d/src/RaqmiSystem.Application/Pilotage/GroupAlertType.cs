namespace RaqmiSystem.Application.Pilotage;

public enum GroupAlertType
{
    /// <summary>Past business days of the period without a DailyClosing at status Closed.</summary>
    UnclosedDays = 1,

    /// <summary>Daily revenue entries at status Submitted for more than 48 hours.</summary>
    PendingValidation = 2,

    /// <summary>Issued unpaid invoices older than 60 days (aging brackets 61-90 and 90+).</summary>
    OverdueInvoices = 3
}
