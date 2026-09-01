using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class AccountingCoreService(RaqmiDbContext db) : IAccountingCoreService
{
    public async Task<IReadOnlyCollection<FiscalYearResponse>> ListFiscalYearsAsync(CancellationToken ct) =>
        await db.FiscalYears.AsNoTracking().OrderByDescending(x=>x.StartsOn).Select(x=>new FiscalYearResponse(x.Id,x.Code,x.StartsOn,x.EndsOn,x.Status)).ToArrayAsync(ct);
    public async Task<ApplicationResult<FiscalYearResponse>> CreateFiscalYearAsync(CreateFiscalYearRequest r, OperationContext c, CancellationToken ct)
    {
        FiscalYear year; try { year=new(r.Code,r.StartsOn,r.EndsOn); } catch(Exception e) when(e is ArgumentException) { return ApplicationResult<FiscalYearResponse>.Validation(e.Message); }
        if(await db.FiscalYears.AnyAsync(x=>x.Code==year.Code || (x.StartsOn<=r.EndsOn && x.EndsOn>=r.StartsOn),ct)) return ApplicationResult<FiscalYearResponse>.Conflict("Fiscal year code or dates overlap an existing year.");
        year.MarkCreated(c.UserName,DateTimeOffset.UtcNow); db.FiscalYears.Add(year);
        if(r.CreateMonthlyPeriods) { var start=r.StartsOn; var n=1; while(start<=r.EndsOn) { var end=new DateOnly(start.Year,start.Month,DateTime.DaysInMonth(start.Year,start.Month)); if(end>r.EndsOn) end=r.EndsOn; var p=new AccountingPeriod(year.Id,n++,start,end); p.MarkCreated(c.UserName,DateTimeOffset.UtcNow); db.AccountingPeriods.Add(p); start=end.AddDays(1); } }
        await db.SaveChangesAsync(ct); return ApplicationResult<FiscalYearResponse>.Success(new(year.Id,year.Code,year.StartsOn,year.EndsOn,year.Status));
    }
    public async Task<ApplicationResult<FiscalYearResponse>> CloseFiscalYearAsync(Guid id, OperationContext c, CancellationToken ct)
    {
        var y=await db.FiscalYears.SingleOrDefaultAsync(x=>x.Id==id,ct); if(y is null)return ApplicationResult<FiscalYearResponse>.NotFound("Fiscal year was not found.");
        if(await db.AccountingPeriods.AnyAsync(x=>x.FiscalYearId==id && x.Status==AccountingPeriodStatus.Open,ct))return ApplicationResult<FiscalYearResponse>.Conflict("Close every period before closing the fiscal year.");
        y.Close(c.UserName,DateTimeOffset.UtcNow); y.MarkUpdated(c.UserName,DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); return ApplicationResult<FiscalYearResponse>.Success(new(y.Id,y.Code,y.StartsOn,y.EndsOn,y.Status));
    }
    public async Task<IReadOnlyCollection<AccountingPeriodResponse>> ListPeriodsAsync(Guid id,CancellationToken ct)=>await db.AccountingPeriods.AsNoTracking().Where(x=>x.FiscalYearId==id).OrderBy(x=>x.Number).Select(x=>new AccountingPeriodResponse(x.Id,x.FiscalYearId,x.Number,x.StartsOn,x.EndsOn,x.Status)).ToArrayAsync(ct);
    public async Task<ApplicationResult<AccountingPeriodResponse>> ClosePeriodAsync(Guid id,OperationContext c,CancellationToken ct)
    {
        var p=await db.AccountingPeriods.SingleOrDefaultAsync(x=>x.Id==id,ct); if(p is null)return ApplicationResult<AccountingPeriodResponse>.NotFound("Accounting period was not found.");
        if(await db.JournalEntries.AnyAsync(x=>x.EntryDate>=p.StartsOn && x.EntryDate<=p.EndsOn && x.Status==EntryStatus.Draft,ct))return ApplicationResult<AccountingPeriodResponse>.Conflict("A period with draft entries cannot be closed.");
        p.Close(c.UserName,DateTimeOffset.UtcNow); p.MarkUpdated(c.UserName,DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); return ApplicationResult<AccountingPeriodResponse>.Success(new(p.Id,p.FiscalYearId,p.Number,p.StartsOn,p.EndsOn,p.Status));
    }
    public async Task<IReadOnlyCollection<PartyResponse>> ListPartiesAsync(CancellationToken ct)=>await db.AccountingParties.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new PartyResponse(x.Id,x.Code,x.Name,x.Kind,x.IsActive)).ToArrayAsync(ct);
    public async Task<ApplicationResult<PartyResponse>> CreatePartyAsync(CreatePartyRequest r,OperationContext c,CancellationToken ct) { AccountingParty p; try{p=new(r.Code,r.Name,r.Kind);}catch(ArgumentException e){return ApplicationResult<PartyResponse>.Validation(e.Message);} if(await db.AccountingParties.AnyAsync(x=>x.Code==p.Code,ct))return ApplicationResult<PartyResponse>.Conflict("Party code already exists."); p.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.AccountingParties.Add(p);await db.SaveChangesAsync(ct);return ApplicationResult<PartyResponse>.Success(new(p.Id,p.Code,p.Name,p.Kind,p.IsActive)); }
    public async Task<ApplicationResult<ReconciliationResponse>> ReconcileAsync(CreateReconciliationRequest r,OperationContext c,CancellationToken ct)
    {
        var ids=r.Debits.Concat(r.Credits).Select(x=>x.JournalEntryLineId).Distinct().ToArray(); var posted=await db.JournalEntryLines.Where(x=>ids.Contains(x.Id)).Join(db.JournalEntries.Where(x=>x.Status==EntryStatus.Posted),l=>l.JournalEntryId,e=>e.Id,(l,e)=>l.Id).CountAsync(ct); if(posted!=ids.Length)return ApplicationResult<ReconciliationResponse>.Validation("Every reconciled line must belong to a posted entry.");
        try { var allocations=r.Debits.Select(x=>new ReconciliationAllocation(x.JournalEntryLineId,ReconciliationSide.Debit,x.Amount)).Concat(r.Credits.Select(x=>new ReconciliationAllocation(x.JournalEntryLineId,ReconciliationSide.Credit,x.Amount))).ToArray(); var rec=new Reconciliation(r.Code,r.PartyId,allocations); rec.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.Reconciliations.Add(rec);await db.SaveChangesAsync(ct);return ApplicationResult<ReconciliationResponse>.Success(new(rec.Id,rec.Code,rec.PartyId,rec.MatchedAmount,rec.Status)); } catch(Exception e) when(e is ArgumentException or InvalidOperationException or DbUpdateException){return ApplicationResult<ReconciliationResponse>.Validation(e.Message);}
    }
    public async Task<IReadOnlyCollection<GeneralLedgerRow>> GetGeneralLedgerAsync(string code,DateOnly? fromDate,DateOnly? toDate,CancellationToken ct)
    { var q=from l in db.JournalEntryLines.AsNoTracking() join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id where l.AccountCode==code && e.Status==EntryStatus.Posted && (!fromDate.HasValue||e.EntryDate>=fromDate) && (!toDate.HasValue||e.EntryDate<=toDate) orderby e.EntryDate,e.PostedAt,l.LineNumber select new{e.EntryDate,e.JournalCode,EntryId=e.Id,e.Label,e.Reference,l.Debit,l.Credit}; var rows=await q.ToArrayAsync(ct);decimal balance=0;return rows.Select(x=>{balance+=x.Debit-x.Credit;return new GeneralLedgerRow(x.EntryDate,x.JournalCode,x.EntryId,x.Label,x.Reference,x.Debit,x.Credit,balance);}).ToArray(); }
    public async Task<int> SeedScfAsync(OperationContext c,CancellationToken ct)
    { var seed=new[]{("101000","Capital émis",AccountKind.Equity),("401000","Fournisseurs",AccountKind.Liability),("411000","Clients",AccountKind.Asset),("512000","Banques",AccountKind.Asset),("530000","Caisse",AccountKind.Asset),("607000","Achats de marchandises",AccountKind.Expense),("706000","Prestations de services",AccountKind.Revenue)};var count=0;foreach(var x in seed)if(!await db.ChartAccounts.AnyAsync(a=>a.Code==x.Item1,ct)){var a=new ChartAccount(x.Item1,x.Item2,x.Item3);a.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.ChartAccounts.Add(a);count++;}await db.SaveChangesAsync(ct);return count; }
}
