using System.Data;
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

    /// <summary>
    /// Ouvre une periode dans un exercice. Sans cette operation, un exercice cree avec
    /// CreateMonthlyPeriods=false etait definitivement inutilisable : rien ne se comptabilise hors
    /// d'une periode ouverte (AccountingService.RequireOpenPeriodAsync), et la seule reparation
    /// etait un INSERT a la main en production.
    /// </summary>
    public async Task<ApplicationResult<AccountingPeriodResponse>> CreatePeriodAsync(
        Guid fiscalYearId,
        CreateAccountingPeriodRequest request,
        OperationContext context,
        CancellationToken ct)
    {
        var year = await db.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, ct);

        if (year is null)
        {
            return ApplicationResult<AccountingPeriodResponse>.NotFound("Fiscal year was not found.");
        }

        if (year.Status == FiscalYearStatus.Closed)
        {
            return ApplicationResult<AccountingPeriodResponse>.Conflict("A closed fiscal year cannot receive new periods.");
        }

        AccountingPeriod period;

        try
        {
            period = new AccountingPeriod(fiscalYearId, request.Number, request.StartsOn, request.EndsOn);
        }
        catch (ArgumentException e)
        {
            return ApplicationResult<AccountingPeriodResponse>.Validation(e.Message);
        }

        if (request.StartsOn < year.StartsOn || request.EndsOn > year.EndsOn)
        {
            return ApplicationResult<AccountingPeriodResponse>.Validation("The period must lie within the fiscal year.");
        }

        var siblings = await db.AccountingPeriods.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .Select(x => new { x.Number, x.StartsOn, x.EndsOn })
            .ToArrayAsync(ct);

        if (siblings.Any(x => x.Number == request.Number))
        {
            return ApplicationResult<AccountingPeriodResponse>.Conflict("A period with this number already exists in the fiscal year.");
        }

        // Deux periodes qui se chevauchent rendraient RequireOpenPeriodAsync ambigu (SingleOrDefault
        // sur la date) : l'ecriture ne saurait plus a quelle periode elle appartient.
        if (siblings.Any(x => x.StartsOn <= request.EndsOn && x.EndsOn >= request.StartsOn))
        {
            return ApplicationResult<AccountingPeriodResponse>.Conflict("The period overlaps an existing period of the fiscal year.");
        }

        var now = DateTimeOffset.UtcNow;
        period.MarkCreated(context.UserName, now);
        db.AccountingPeriods.Add(period);

        await WriteAuditAsync(
            "accounting.period.created",
            period.Id,
            context,
            new { period.FiscalYearId, period.Number, period.StartsOn, period.EndsOn },
            ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            // Deux ouvertures simultanees du meme numero : l'index unique (exercice, numero) tranche.
            return ApplicationResult<AccountingPeriodResponse>.Conflict("A period with this number already exists in the fiscal year.");
        }

        return ApplicationResult<AccountingPeriodResponse>.Success(
            new(period.Id, period.FiscalYearId, period.Number, period.StartsOn, period.EndsOn, period.Status));
    }

    public async Task<ApplicationResult<AccountingPeriodResponse>> ClosePeriodAsync(Guid id,OperationContext c,CancellationToken ct)
    {
        var p=await db.AccountingPeriods.SingleOrDefaultAsync(x=>x.Id==id,ct); if(p is null)return ApplicationResult<AccountingPeriodResponse>.NotFound("Accounting period was not found.");
        if(await db.JournalEntries.AnyAsync(x=>x.EntryDate>=p.StartsOn && x.EntryDate<=p.EndsOn && x.Status==EntryStatus.Draft,ct))return ApplicationResult<AccountingPeriodResponse>.Conflict("A period with draft entries cannot be closed.");
        p.Close(c.UserName,DateTimeOffset.UtcNow); p.MarkUpdated(c.UserName,DateTimeOffset.UtcNow); await WriteAuditAsync("accounting.period.closed",p.Id,c,new{p.FiscalYearId,p.Number,p.StartsOn,p.EndsOn},ct); await db.SaveChangesAsync(ct); return ApplicationResult<AccountingPeriodResponse>.Success(new(p.Id,p.FiscalYearId,p.Number,p.StartsOn,p.EndsOn,p.Status));
    }
    public async Task<IReadOnlyCollection<PartyResponse>> ListPartiesAsync(CancellationToken ct)=>await db.AccountingParties.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new PartyResponse(x.Id,x.Code,x.Name,x.Kind,x.IsActive)).ToArrayAsync(ct);
    public async Task<ApplicationResult<PartyResponse>> CreatePartyAsync(CreatePartyRequest r,OperationContext c,CancellationToken ct) { AccountingParty p; try{p=new(r.Code,r.Name,r.Kind);}catch(ArgumentException e){return ApplicationResult<PartyResponse>.Validation(e.Message);} if(await db.AccountingParties.AnyAsync(x=>x.Code==p.Code,ct))return ApplicationResult<PartyResponse>.Conflict("Party code already exists."); p.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.AccountingParties.Add(p);await WriteAuditAsync("accounting.party.created",p.Id,c,new{p.Code,p.Name,p.Kind},ct);await db.SaveChangesAsync(ct);return ApplicationResult<PartyResponse>.Success(new(p.Id,p.Code,p.Name,p.Kind,p.IsActive)); }

    /// <summary>
    /// Lettre des lignes d'un tiers entre elles.
    ///
    /// Lecture des lignes, lecture des allocations deja posees et insertion tiennent dans UNE
    /// transaction Serializable : c'etait la seule operation financiere du depot sans transaction,
    /// avec un index NON unique sur journal_entry_line_id. Deux lettrages simultanes de la meme
    /// ligne lisaient tous deux « rien d'alloue », passaient tous les deux, et la ligne finissait
    /// lettree deux fois. Sous Serializable, le second echoue en erreur de serialisation et est
    /// rendu en conflit rejouable, comme partout ailleurs (AccountingService).
    /// </summary>
    public async Task<ApplicationResult<ReconciliationResponse>> ReconcileAsync(CreateReconciliationRequest r, OperationContext c, CancellationToken ct)
    {
        var requested = r.Debits.Select(x => (Allocation: x, Side: ReconciliationSide.Debit))
            .Concat(r.Credits.Select(x => (Allocation: x, Side: ReconciliationSide.Credit)))
            .ToArray();

        var ids = requested.Select(x => x.Allocation.JournalEntryLineId).Distinct().ToArray();

        if (ids.Length != requested.Length)
        {
            return ApplicationResult<ReconciliationResponse>.Validation("A line can occur only once in one reconciliation.");
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var lines = await db.JournalEntryLines
                .Where(x => ids.Contains(x.Id))
                .Join(db.JournalEntries.Where(x => x.Status == EntryStatus.Posted), l => l.JournalEntryId, e => e.Id, (l, e) => l)
                .ToArrayAsync(ct);

            if (lines.Length != ids.Length || lines.Any(x => x.PartyId != r.PartyId))
            {
                return ApplicationResult<ReconciliationResponse>.Validation("Every reconciled line must be posted and belong to the selected party.");
            }

            var already = await db.Set<ReconciliationAllocation>()
                .Where(x => ids.Contains(x.JournalEntryLineId))
                .GroupBy(x => x.JournalEntryLineId)
                .Select(x => new { x.Key, Amount = x.Sum(a => a.Amount) })
                .ToDictionaryAsync(x => x.Key, x => x.Amount, ct);

            foreach (var item in requested)
            {
                var line = lines.Single(x => x.Id == item.Allocation.JournalEntryLineId);
                var sideAmount = item.Side == ReconciliationSide.Debit ? line.Debit : line.Credit;

                if (sideAmount <= 0)
                {
                    return ApplicationResult<ReconciliationResponse>.Validation("Allocation side does not match the accounting line.");
                }

                if (already.GetValueOrDefault(line.Id) + item.Allocation.Amount > sideAmount)
                {
                    return ApplicationResult<ReconciliationResponse>.Validation("Allocated amount exceeds the outstanding movement.");
                }
            }

            var allocations = requested
                .Select(x => new ReconciliationAllocation(x.Allocation.JournalEntryLineId, x.Side, x.Allocation.Amount))
                .ToArray();

            var rec = new Reconciliation(r.Code, r.PartyId, allocations);
            rec.MarkCreated(c.UserName, DateTimeOffset.UtcNow);
            db.Reconciliations.Add(rec);

            await WriteAuditAsync(
                "accounting.reconciliation.created",
                rec.Id,
                c,
                new { rec.Code, rec.PartyId, rec.MatchedAmount, rec.Status, LineCount = allocations.Length },
                ct);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ApplicationResult<ReconciliationResponse>.Success(new(rec.Id, rec.Code, rec.PartyId, rec.MatchedAmount, rec.Status));
        }
        catch (Exception e) when (e.IsSerializationFailure())
        {
            return ApplicationResult<ReconciliationResponse>.Conflict("A concurrent reconciliation touched the same lines. Reload and retry.");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            return ApplicationResult<ReconciliationResponse>.Validation(e.Message);
        }
    }

    public async Task<IReadOnlyCollection<GeneralLedgerRow>> GetGeneralLedgerAsync(string code,DateOnly? fromDate,DateOnly? toDate,CancellationToken ct)
    { var q=from l in db.JournalEntryLines.AsNoTracking() join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id where l.AccountCode==code && e.Status==EntryStatus.Posted && (!fromDate.HasValue||e.EntryDate>=fromDate) && (!toDate.HasValue||e.EntryDate<=toDate) orderby e.EntryDate,e.PostedAt,l.LineNumber select new{e.EntryDate,e.JournalCode,EntryId=e.Id,e.Label,e.Reference,l.Debit,l.Credit}; var rows=await q.ToArrayAsync(ct);decimal balance=0;return rows.Select(x=>{balance+=x.Debit-x.Credit;return new GeneralLedgerRow(x.EntryDate,x.JournalCode,x.EntryId,x.Label,x.Reference,x.Debit,x.Credit,balance);}).ToArray(); }

    /// <summary>
    /// Balance auxiliaire : par tiers, les mouvements de la fenetre, le montant lettre et le reste
    /// non lettre.
    ///
    /// Le montant LETTRE d'un tiers n'est pas la somme de toutes ses allocations : un lettrage
    /// rapproche un debit d'un credit, et compter les deux cotes doublait le chiffre - un client a
    /// 1 500 de factures et 1 000 d'encaissement, lettre a 1 000, affichait 2 000 de lettre et
    /// « rien a recouvrer » alors qu'il doit 500. Il est compte d'UN seul cote : pour chaque
    /// lettrage, ce qui a trouve sa contrepartie de l'autre cote (min des debits et des credits
    /// alloues - un lettrage partiel de 100 contre 60 ne lettre que 60, c'est aussi son
    /// MatchedAmount). Pour un lettrage equilibre, c'est exactement la somme du cote naturel du
    /// tiers : debit pour un client, credit pour un fournisseur.
    ///
    /// Le RESTE (Outstanding) est le non-lettre du cote naturel du tiers, deduit de PartyKind :
    /// factures non soldees d'un client (debit), factures non reglees d'un fournisseur (credit) ;
    /// pour un tiers « autre », le cote de son solde. Il se distingue du solde : un client dont
    /// l'encaissement n'est pas encore lettre a un solde de 500 et 1 500 de factures ouvertes.
    ///
    /// Les allocations sont prises sur les memes lignes que les mouvements (ecritures
    /// comptabilisees, meme fenetre de dates) pour que les colonnes parlent de la meme chose ;
    /// l'ancienne version ignorait la fenetre.
    /// </summary>
    public async Task<IReadOnlyCollection<AuxiliaryBalanceRow>> GetAuxiliaryBalanceAsync(DateOnly? fromDate, DateOnly? toDate, PartyKind? kind, CancellationToken ct)
    {
        var movements = await (
            from l in db.JournalEntryLines.AsNoTracking()
            join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
            join p in db.AccountingParties.AsNoTracking() on l.PartyId equals p.Id
            where e.Status == EntryStatus.Posted
                && (!fromDate.HasValue || e.EntryDate >= fromDate)
                && (!toDate.HasValue || e.EntryDate <= toDate)
                && (!kind.HasValue || p.Kind == kind)
            group l by new { p.Id, p.Code, p.Name, p.Kind } into g
            select new { g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToArrayAsync(ct);

        var allocations = await (
            from a in db.Set<ReconciliationAllocation>().AsNoTracking()
            join l in db.JournalEntryLines.AsNoTracking() on a.JournalEntryLineId equals l.Id
            join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
            where l.PartyId.HasValue
                && e.Status == EntryStatus.Posted
                && (!fromDate.HasValue || e.EntryDate >= fromDate)
                && (!toDate.HasValue || e.EntryDate <= toDate)
            group a by new { PartyId = l.PartyId!.Value, a.ReconciliationId, a.Side } into g
            select new { g.Key.PartyId, g.Key.ReconciliationId, g.Key.Side, Amount = g.Sum(x => x.Amount) })
            .ToArrayAsync(ct);

        var reconciledByParty = allocations
            .GroupBy(x => new { x.PartyId, x.ReconciliationId })
            .Select(g => new
            {
                g.Key.PartyId,
                Matched = Math.Min(
                    g.Where(x => x.Side == ReconciliationSide.Debit).Sum(x => x.Amount),
                    g.Where(x => x.Side == ReconciliationSide.Credit).Sum(x => x.Amount))
            })
            .GroupBy(x => x.PartyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Matched));

        return movements.Select(x =>
        {
            var balance = x.Debit - x.Credit;
            var reconciled = reconciledByParty.GetValueOrDefault(x.Key.Id);

            var naturalSide = x.Key.Kind switch
            {
                PartyKind.Customer => x.Debit,
                PartyKind.Supplier => x.Credit,
                _ => balance >= 0 ? x.Debit : x.Credit
            };

            return new AuxiliaryBalanceRow(
                x.Key.Id,
                x.Key.Code,
                x.Key.Name,
                x.Key.Kind,
                x.Debit,
                x.Credit,
                balance,
                reconciled,
                Math.Max(0, naturalSide - reconciled));
        }).ToArray();
    }

    public async Task<int> SeedScfAsync(OperationContext c,CancellationToken ct)
    { var seed=new[]{("101000","Capital émis",AccountKind.Equity),("401000","Fournisseurs",AccountKind.Liability),("411000","Clients",AccountKind.Asset),("512000","Banques",AccountKind.Asset),("530000","Caisse",AccountKind.Asset),("607000","Achats de marchandises",AccountKind.Expense),("706000","Prestations de services",AccountKind.Revenue)};var count=0;foreach(var x in seed)if(!await db.ChartAccounts.AnyAsync(a=>a.Code==x.Item1,ct)){var a=new ChartAccount(x.Item1,x.Item2,x.Item3);a.MarkCreated(c.UserName,DateTimeOffset.UtcNow);db.ChartAccounts.Add(a);count++;}await WriteAuditAsync("accounting.scf.seeded",Guid.Empty,c,new{Inserted=count},ct);await db.SaveChangesAsync(ct);return count; }
    private Task WriteAuditAsync(string action,Guid id,OperationContext c,object details,CancellationToken ct)=>audit.WriteAsync(new AuditLogEntry(c.UserId,c.UserName,action,"accounting.core",id.ToString(),c.IpAddress,System.Text.Json.JsonSerializer.Serialize(details)),ct);
}
