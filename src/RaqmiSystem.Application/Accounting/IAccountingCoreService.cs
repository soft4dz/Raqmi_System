using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

public sealed record CreateFiscalYearRequest(string Code, DateOnly StartsOn, DateOnly EndsOn, bool CreateMonthlyPeriods = true);
public sealed record FiscalYearResponse(Guid Id, string Code, DateOnly StartsOn, DateOnly EndsOn, FiscalYearStatus Status);
public sealed record AccountingPeriodResponse(Guid Id, Guid FiscalYearId, int Number, DateOnly StartsOn, DateOnly EndsOn, AccountingPeriodStatus Status);
public sealed record CreatePartyRequest(string Code, string Name, PartyKind Kind);
public sealed record PartyResponse(Guid Id, string Code, string Name, PartyKind Kind, bool IsActive);
public sealed record ReconcileAllocationRequest(Guid JournalEntryLineId, decimal Amount);
public sealed record CreateReconciliationRequest(string Code, Guid PartyId, IReadOnlyCollection<ReconcileAllocationRequest> Debits, IReadOnlyCollection<ReconcileAllocationRequest> Credits);
public sealed record ReconciliationResponse(Guid Id, string Code, Guid PartyId, decimal MatchedAmount, ReconciliationStatus Status);
public sealed record GeneralLedgerRow(DateOnly Date, string JournalCode, Guid EntryId, string Label, string? Reference, decimal Debit, decimal Credit, decimal RunningBalance);
public sealed record AuxiliaryBalanceRow(Guid PartyId, string PartyCode, string PartyName, PartyKind Kind, decimal Debit, decimal Credit, decimal Balance, decimal Reconciled, decimal Outstanding);

public interface IAccountingCoreService
{
    Task<IReadOnlyCollection<FiscalYearResponse>> ListFiscalYearsAsync(CancellationToken ct);
    Task<ApplicationResult<FiscalYearResponse>> CreateFiscalYearAsync(CreateFiscalYearRequest request, OperationContext context, CancellationToken ct);
    Task<ApplicationResult<FiscalYearResponse>> CloseFiscalYearAsync(Guid id, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<AccountingPeriodResponse>> ListPeriodsAsync(Guid fiscalYearId, CancellationToken ct);
    Task<ApplicationResult<AccountingPeriodResponse>> ClosePeriodAsync(Guid id, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<PartyResponse>> ListPartiesAsync(CancellationToken ct);
    Task<ApplicationResult<PartyResponse>> CreatePartyAsync(CreatePartyRequest request, OperationContext context, CancellationToken ct);
    Task<ApplicationResult<ReconciliationResponse>> ReconcileAsync(CreateReconciliationRequest request, OperationContext context, CancellationToken ct);
    Task<IReadOnlyCollection<GeneralLedgerRow>> GetGeneralLedgerAsync(string accountCode, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<IReadOnlyCollection<AuxiliaryBalanceRow>> GetAuxiliaryBalanceAsync(DateOnly? from, DateOnly? to, PartyKind? kind, CancellationToken ct);
    Task<int> SeedScfAsync(OperationContext context, CancellationToken ct);
}
