using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Accounting;

public enum FiscalYearStatus { Open, Closed }
public enum AccountingPeriodStatus { Open, Closed }
public enum PartyKind { Customer, Supplier, Other }
public enum ReconciliationStatus { Partial, Complete }

public sealed class FiscalYear : AuditableEntity
{
    private FiscalYear() { }
    public FiscalYear(string code, DateOnly startsOn, DateOnly endsOn)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Fiscal year code is required.", nameof(code));
        if (endsOn < startsOn) throw new ArgumentException("Fiscal year end must be after its start.", nameof(endsOn));
        Code = code.Trim().ToUpperInvariant(); StartsOn = startsOn; EndsOn = endsOn;
    }
    public string Code { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public FiscalYearStatus Status { get; private set; } = FiscalYearStatus.Open;
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }
    public void Close(string actor, DateTimeOffset now)
    {
        if (Status == FiscalYearStatus.Closed) throw new InvalidOperationException("Fiscal year is already closed.");
        Status = FiscalYearStatus.Closed; ClosedAt = now; ClosedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
    }
}

public sealed class AccountingPeriod : AuditableEntity
{
    private AccountingPeriod() { }
    public AccountingPeriod(Guid fiscalYearId, int number, DateOnly startsOn, DateOnly endsOn)
    {
        if (number is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(number));
        if (endsOn < startsOn) throw new ArgumentException("Period end must be after its start.", nameof(endsOn));
        FiscalYearId = fiscalYearId; Number = number; StartsOn = startsOn; EndsOn = endsOn;
    }
    public Guid FiscalYearId { get; private set; }
    public int Number { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public AccountingPeriodStatus Status { get; private set; } = AccountingPeriodStatus.Open;
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }
    public void Close(string actor, DateTimeOffset now)
    {
        if (Status == AccountingPeriodStatus.Closed) throw new InvalidOperationException("Accounting period is already closed.");
        Status = AccountingPeriodStatus.Closed; ClosedAt = now; ClosedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
    }
}

public sealed class AccountingParty : AuditableEntity
{
    private AccountingParty() { }
    public AccountingParty(string code, string name, PartyKind kind)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Party code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Party name is required.", nameof(name));
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); Kind = kind;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PartyKind Kind { get; private set; }
    public bool IsActive { get; private set; } = true;
}

/// <summary>One row per journal/year. The database row is locked while the next number is allocated.</summary>
public sealed class JournalSequence
{
    private JournalSequence() { }
    public JournalSequence(string journalCode, Guid fiscalYearId) { JournalCode = journalCode; FiscalYearId = fiscalYearId; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string JournalCode { get; private set; } = string.Empty;
    public Guid FiscalYearId { get; private set; }
    public long LastNumber { get; private set; }
    public long Next() => ++LastNumber;
}

public sealed class Reconciliation : AuditableEntity
{
    private readonly List<ReconciliationAllocation> _allocations = new();
    private Reconciliation() { }
    public Reconciliation(string code, Guid partyId, IEnumerable<ReconciliationAllocation> allocations)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Reconciliation code is required.", nameof(code));
        Code = code.Trim().ToUpperInvariant(); PartyId = partyId;
        _allocations.AddRange(allocations ?? throw new ArgumentNullException(nameof(allocations)));
        if (_allocations.Count < 2 || _allocations.Any(x => x.Amount <= 0))
            throw new InvalidOperationException("A reconciliation needs at least two positive allocations.");
        var debit = _allocations.Where(x => x.Side == ReconciliationSide.Debit).Sum(x => x.Amount);
        var credit = _allocations.Where(x => x.Side == ReconciliationSide.Credit).Sum(x => x.Amount);
        MatchedAmount = Math.Min(debit, credit);
        if (MatchedAmount <= 0) throw new InvalidOperationException("A reconciliation must match debit and credit movements.");
        Status = debit == credit ? ReconciliationStatus.Complete : ReconciliationStatus.Partial;
    }
    public string Code { get; private set; } = string.Empty;
    public Guid PartyId { get; private set; }
    public decimal MatchedAmount { get; private set; }
    public ReconciliationStatus Status { get; private set; }
    public IReadOnlyCollection<ReconciliationAllocation> Allocations => _allocations.AsReadOnly();
}

public enum ReconciliationSide { Debit, Credit }
public sealed class ReconciliationAllocation
{
    private ReconciliationAllocation() { }
    public ReconciliationAllocation(Guid journalEntryLineId, ReconciliationSide side, decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        JournalEntryLineId = journalEntryLineId; Side = side; Amount = amount;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ReconciliationId { get; private set; }
    public Guid JournalEntryLineId { get; private set; }
    public ReconciliationSide Side { get; private set; }
    public decimal Amount { get; private set; }
}
