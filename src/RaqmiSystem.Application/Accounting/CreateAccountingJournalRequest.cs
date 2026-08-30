namespace RaqmiSystem.Application.Accounting;

public sealed record CreateAccountingJournalRequest(
    string Code,
    string Label);
