using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class AccountingCoreService(RaqmiDbContext db, IAuditLogWriter audit) : IAccountingCoreService
{
    public async Task<IReadOnlyCollection<FiscalYearResponse>> ListFiscalYearsAsync(CancellationToken ct) =>
        await db.FiscalYears.AsNoTracking().OrderByDescending(x=>x.StartsOn).Select(x=>new FiscalYearResponse(x.Id,x.Code,x.StartsOn,x.EndsOn,x.Status)).ToArrayAsync(ct);
    public async Task<ApplicationResult<FiscalYearResponse>> CreateFiscalYearAsync(CreateFiscalYearRequest r, OperationContext c, CancellationToken ct)
    {
        FiscalYear year; try { year=new(r.Code,r.StartsOn,r.EndsOn); } catch(Exception e) when(e is ArgumentException) { return ApplicationResult<FiscalYearResponse>.Validation(e.Message); }
        if(await db.FiscalYears.AnyAsync(x=>x.Code==year.Code || (x.StartsOn<=r.EndsOn && x.EndsOn>=r.StartsOn),ct)) return ApplicationResult<FiscalYearResponse>.Conflict("Fiscal year code or dates overlap an existing year.");
        year.MarkCreated(c.UserName,DateTimeOffset.UtcNow); db.FiscalYears.Add(year);
        if(r.CreateMonthlyPeriods) { var start=r.StartsOn; var n=1; while(start<=r.EndsOn) { var end=new DateOnly(start.Year,start.Month,DateTime.DaysInMonth(start.Year,start.Month)); if(end>r.EndsOn) end=r.EndsOn; var p=new AccountingPeriod(year.Id,n++,start,end); p.MarkCreated(c.UserName,DateTimeOffset.UtcNow); db.AccountingPeriods.Add(p); start=end.AddDays(1); } }
        await WriteAuditAsync("accounting.fiscal_year.created",year.Id,c,new{year.Code,year.StartsOn,year.EndsOn},ct); await db.SaveChangesAsync(ct); return ApplicationResult<FiscalYearResponse>.Success(new(year.Id,year.Code,year.StartsOn,year.EndsOn,year.Status));
    }
    public async Task<ApplicationResult<FiscalYearResponse>> CloseFiscalYearAsync(Guid id, OperationContext c, CancellationToken ct)
    {
        var y=await db.FiscalYears.SingleOrDefaultAsync(x=>x.Id==id,ct); if(y is null)return ApplicationResult<FiscalYearResponse>.NotFound("Fiscal year was not found.");
        if(await db.AccountingPeriods.AnyAsync(x=>x.FiscalYearId==id && x.Status==AccountingPeriodStatus.Open,ct))return ApplicationResult<FiscalYearResponse>.Conflict("Close every period before closing the fiscal year.");
        y.Close(c.UserName,DateTimeOffset.UtcNow); y.MarkUpdated(c.UserName,DateTimeOffset.UtcNow); await WriteAuditAsync("accounting.fiscal_year.closed",y.Id,c,new{y.Code},ct); await db.SaveChangesAsync(ct); return ApplicationResult<FiscalYearResponse>.Success(new(y.Id,y.Code,y.StartsOn,y.EndsOn,y.Status));
    }
    public async Task<IReadOnlyCollection<AccountingPeriodResponse>> ListPeriodsAsync(Guid id,CancellationToken ct)=>await db.AccountingPeriods.AsNoTracking().Where(x=>x.FiscalYearId==id).OrderBy(x=>x.Number).Select(x=>new AccountingPeriodResponse(x.Id,x.FiscalYearId,x.Number,x.StartsOn,x.EndsOn,x.Status)).ToArrayAsync(ct);
    public async Task<ApplicationResult<AccountingPeriodResponse>> ClosePeriodAsync(Guid id,OperationContext c,CancellationToken ct)
    {
        var p=await db.AccountingPeriods.SingleOrDefaultAsync(x=>x.Id==id,ct); if(p is null)return ApplicationResult<AccountingPeriodResponse>.NotFound("Accounting period was not found.");
        if(await db.JournalEntries.AnyAsync(x=>x.EntryDate>=p.StartsOn && x.EntryDate<=p.EndsOn && x.Status==EntryStatus.Draft,ct))return ApplicationResult<AccountingPeriodResponse>.Conflict("A period with draft entries cannot be closed.");
        p.Close(c.UserName,DateTimeOffset.UtcNow); p.MarkUpdated(c.UserName,DateTimeOffset.UtcNow); await WriteAuditAsync("accounting.period.closed",p.Id,c,new{p.FiscalYearId,p.Number,p.StartsOn,p.EndsOn},ct); await db.SaveChangesAsync(ct); return ApplicationResult<AccountingPeriodResponse>.Success(new(p.Id,p.FiscalYearId,p.Number,p.StartsOn,p.EndsOn,p.Status));
    }
    public async Task<IReadOnlyCollection<PartyResponse>> ListPartiesAsync(CancellationToken ct)=>await db.AccountingParties.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new PartyResponse(x.Id,x.Code,x.Name,x.Kind,x.IsActive)).ToArrayAsync(ct);
    public async Task<ApplicationResult<PartyResponse>> CreatePartyAsync(CreatePartyRequest r,OperationContext c,CancellationToken ct) { AccountingParty p; try{p=new(r.Code,r.Name,r.Kind);}catch(ArgumentException e){return ApplicationResult<PartyResponse>.Validation(e.Message);} if(await db.AccountingParties.AnyAsync(x=>x.Code==p.Code,ct))return ApplicationResult<PartyResponse>.Conflict("Party code already exists."); p.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.AccountingParties.Add(p);await WriteAuditAsync("accounting.party.created",p.Id,c,new{p.Code,p.Name,p.Kind},ct);await db.SaveChangesAsync(ct);return ApplicationResult<PartyResponse>.Success(new(p.Id,p.Code,p.Name,p.Kind,p.IsActive)); }
    public async Task<ApplicationResult<ReconciliationResponse>> ReconcileAsync(CreateReconciliationRequest r,OperationContext c,CancellationToken ct)
    {
        var requested=r.Debits.Select(x=>(x,Side:ReconciliationSide.Debit)).Concat(r.Credits.Select(x=>(x,Side:ReconciliationSide.Credit))).ToArray();
        var ids=requested.Select(x=>x.x.JournalEntryLineId).Distinct().ToArray(); if(ids.Length!=requested.Length)return ApplicationResult<ReconciliationResponse>.Validation("A line can occur only once in one reconciliation.");
        var lines=await db.JournalEntryLines.Where(x=>ids.Contains(x.Id)).Join(db.JournalEntries.Where(x=>x.Status==EntryStatus.Posted),l=>l.JournalEntryId,e=>e.Id,(l,e)=>l).ToArrayAsync(ct);
        if(lines.Length!=ids.Length || lines.Any(x=>x.PartyId!=r.PartyId))return ApplicationResult<ReconciliationResponse>.Validation("Every reconciled line must be posted and belong to the selected party.");
        var already=await db.Set<ReconciliationAllocation>().Where(x=>ids.Contains(x.JournalEntryLineId)).GroupBy(x=>x.JournalEntryLineId).Select(x=>new{x.Key,Amount=x.Sum(a=>a.Amount)}).ToDictionaryAsync(x=>x.Key,x=>x.Amount,ct);
        foreach(var item in requested){var line=lines.Single(x=>x.Id==item.x.JournalEntryLineId);var sideAmount=item.Side==ReconciliationSide.Debit?line.Debit:line.Credit;if(sideAmount<=0)return ApplicationResult<ReconciliationResponse>.Validation("Allocation side does not match the accounting line.");if(already.GetValueOrDefault(line.Id)+item.x.Amount>sideAmount)return ApplicationResult<ReconciliationResponse>.Validation("Allocated amount exceeds the outstanding movement.");}
        try { var allocations=requested.Select(x=>new ReconciliationAllocation(x.x.JournalEntryLineId,x.Side,x.x.Amount)).ToArray(); var rec=new Reconciliation(r.Code,r.PartyId,allocations); rec.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.Reconciliations.Add(rec);await WriteAuditAsync("accounting.reconciliation.created",rec.Id,c,new{rec.Code,rec.PartyId,rec.MatchedAmount,rec.Status,LineCount=allocations.Length},ct);await db.SaveChangesAsync(ct);return ApplicationResult<ReconciliationResponse>.Success(new(rec.Id,rec.Code,rec.PartyId,rec.MatchedAmount,rec.Status)); } catch(Exception e) when(e is ArgumentException or InvalidOperationException or DbUpdateException){return ApplicationResult<ReconciliationResponse>.Validation(e.Message);}
    }
    public async Task<IReadOnlyCollection<GeneralLedgerRow>> GetGeneralLedgerAsync(string code,DateOnly? fromDate,DateOnly? toDate,CancellationToken ct)
    { var q=from l in db.JournalEntryLines.AsNoTracking() join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id where l.AccountCode==code && e.Status==EntryStatus.Posted && (!fromDate.HasValue||e.EntryDate>=fromDate) && (!toDate.HasValue||e.EntryDate<=toDate) orderby e.EntryDate,e.PostedAt,l.LineNumber select new{e.EntryDate,e.JournalCode,EntryId=e.Id,e.Label,e.Reference,l.Debit,l.Credit}; var rows=await q.ToArrayAsync(ct);decimal balance=0;return rows.Select(x=>{balance+=x.Debit-x.Credit;return new GeneralLedgerRow(x.EntryDate,x.JournalCode,x.EntryId,x.Label,x.Reference,x.Debit,x.Credit,balance);}).ToArray(); }
    public async Task<IReadOnlyCollection<AuxiliaryBalanceRow>> GetAuxiliaryBalanceAsync(DateOnly? fromDate,DateOnly? toDate,PartyKind? kind,CancellationToken ct)
    {
        var movements=await (from l in db.JournalEntryLines.AsNoTracking() join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id join p in db.AccountingParties.AsNoTracking() on l.PartyId equals p.Id where e.Status==EntryStatus.Posted && (!fromDate.HasValue||e.EntryDate>=fromDate) && (!toDate.HasValue||e.EntryDate<=toDate) && (!kind.HasValue||p.Kind==kind) group l by new{p.Id,p.Code,p.Name,p.Kind} into g select new{g.Key,Debit=g.Sum(x=>x.Debit),Credit=g.Sum(x=>x.Credit)}).ToArrayAsync(ct);
        var reconciled=await (from a in db.Set<ReconciliationAllocation>().AsNoTracking() join l in db.JournalEntryLines.AsNoTracking() on a.JournalEntryLineId equals l.Id where l.PartyId.HasValue group a by l.PartyId!.Value into g select new{PartyId=g.Key,Amount=g.Sum(x=>x.Amount)}).ToDictionaryAsync(x=>x.PartyId,x=>x.Amount,ct);
        return movements.Select(x=>{var balance=x.Debit-x.Credit;var matched=reconciled.GetValueOrDefault(x.Key.Id);return new AuxiliaryBalanceRow(x.Key.Id,x.Key.Code,x.Key.Name,x.Key.Kind,x.Debit,x.Credit,balance,matched,Math.Max(0,Math.Abs(balance)-matched));}).ToArray();
    }
    public async Task<int> SeedScfAsync(OperationContext c,CancellationToken ct)
    { var seed=new[]{("101000","Capital émis",AccountKind.Equity),("401000","Fournisseurs",AccountKind.Liability),("411000","Clients",AccountKind.Asset),("512000","Banques",AccountKind.Asset),("530000","Caisse",AccountKind.Asset),("607000","Achats de marchandises",AccountKind.Expense),("706000","Prestations de services",AccountKind.Revenue)};var count=0;foreach(var x in seed)if(!await db.ChartAccounts.AnyAsync(a=>a.Code==x.Item1,ct)){var a=new ChartAccount(x.Item1,x.Item2,x.Item3);a.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.ChartAccounts.Add(a);count++;}await WriteAuditAsync("accounting.scf.seeded",Guid.Empty,c,new{Inserted=count},ct);await db.SaveChangesAsync(ct);return count; }
    private Task WriteAuditAsync(string action,Guid id,OperationContext c,object details,CancellationToken ct)=>audit.WriteAsync(new AuditLogEntry(c.UserId,c.UserName,action,"accounting.core",id.ToString(),c.IpAddress,System.Text.Json.JsonSerializer.Serialize(details)),ct);
}
