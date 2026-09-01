using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// SCF accounting endpoints: chart of accounts, journals, entries and trial balance.
///
/// Three permissions, not two. Reading (accounting.read) and capturing (accounting.write) are the
/// usual pair, but POSTING an entry - and reversing one, which posts a correcting entry - sits
/// behind its own key (accounting.post): capture is a draft anyone in the accounting team may
/// prepare, while posting is what actually engages the accounts and can no longer be undone
/// except by another posted entry.
///
/// Depuis le lot 2.1 les routes portent les cles CIBLES du registre (PermissionRegistry) :
/// finance.accounting.read, finance.chart.manage (plan et journaux), finance.entry.manage
/// (brouillons), finance.party.manage, finance.entry.post, finance.entry.reverse,
/// finance.party.reconcile, finance.period.close et finance.accounting.admin. Chaque politique
/// accepte encore la cle historique qui la couvre : accounting.write, composite, vaut les trois
/// cles manage - mais une seule d'entre elles ne vaut jamais accounting.write.
/// </summary>
internal static class AccountingEndpoints
{
    public static RouteGroupBuilder MapAccountingEndpoints(this RouteGroupBuilder api)
    {
        MapChartAccountEndpoints(api);
        MapJournalEndpoints(api);
        MapJournalEntryEndpoints(api);
        MapTrialBalanceEndpoints(api);
        MapAccountingCoreEndpoints(api);
        return api;
    }

    private static void MapAccountingCoreEndpoints(RouteGroupBuilder api)
    {
        var core = api.MapGroup("/accounting").WithTags("SCF accounting core");
        core.MapGet("/fiscal-years", async (IAccountingCoreService s, CancellationToken ct) => Results.Ok(await s.ListFiscalYearsAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
        core.MapPost("/fiscal-years", async (CreateFiscalYearRequest r,IAccountingCoreService s,HttpContext h,CancellationToken ct)=>(await s.CreateFiscalYearAsync(r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.FinanceAccountingAdmin);
        core.MapGet("/fiscal-years/{id:guid}/periods", async (Guid id,IAccountingCoreService s,CancellationToken ct)=>Results.Ok(await s.ListPeriodsAsync(id,ct))).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
        core.MapPost("/fiscal-years/{id:guid}/close", async (Guid id,IAccountingCoreService s,HttpContext h,CancellationToken ct)=>(await s.CloseFiscalYearAsync(id,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.FinancePeriodClose);
        core.MapPost("/periods/{id:guid}/close", async (Guid id,IAccountingCoreService s,HttpContext h,CancellationToken ct)=>(await s.ClosePeriodAsync(id,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.FinancePeriodClose);
        core.MapGet("/parties", async (IAccountingCoreService s,CancellationToken ct)=>Results.Ok(await s.ListPartiesAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
        core.MapPost("/parties", async (CreatePartyRequest r,IAccountingCoreService s,HttpContext h,CancellationToken ct)=>(await s.CreatePartyAsync(r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.FinancePartyManage);
        core.MapPost("/reconciliations", async (CreateReconciliationRequest r,IAccountingCoreService s,HttpContext h,CancellationToken ct)=>(await s.ReconcileAsync(r,h.ToOperationContext(),ct)).ToHttpResult()).RequireAuthorization(PermissionCatalog.FinancePartyReconcile);
        core.MapGet("/general-ledger/{accountCode}", async (string accountCode,DateOnly? from,DateOnly? to,IAccountingCoreService s,CancellationToken ct)=>Results.Ok(await s.GetGeneralLedgerAsync(accountCode,from,to,ct))).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
        core.MapGet("/auxiliary-balance", async (DateOnly? from,DateOnly? to,PartyKind? kind,IAccountingCoreService s,CancellationToken ct)=>Results.Ok(await s.GetAuxiliaryBalanceAsync(from,to,kind,ct))).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
        core.MapPost("/scf/seed", async (IAccountingCoreService s,HttpContext h,CancellationToken ct)=>Results.Ok(new{inserted=await s.SeedScfAsync(h.ToOperationContext(),ct)})).RequireAuthorization(PermissionCatalog.FinanceAccountingAdmin);
    }

    private static void MapChartAccountEndpoints(RouteGroupBuilder api)
    {
        var classes = api.MapGroup("/accounting/account-classes")
            .WithTags("Chart of accounts");

        // The SCF class skeleton. Served from the domain catalog rather than from the database:
        // the seven classes are the nomenclature's structure, not establishment data.
        classes.MapGet("", () =>
        {
            var payload = AccountClassCatalog.All
                .Select(definition => new AccountClassResponse(
                    definition.AccountClass,
                    definition.Label,
                    definition.AllowedKinds))
                .ToArray();

            return Results.Ok(payload);
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        var accounts = api.MapGroup("/accounting/accounts")
            .WithTags("Chart of accounts");

        accounts.MapGet("", async (
            string? search,
            int? accountClass,
            bool? includeInactive,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            if (accountClass.HasValue &&
                (accountClass.Value < AccountClassCatalog.MinAccountClass ||
                    accountClass.Value > AccountClassCatalog.MaxAccountClass))
            {
                return Results.BadRequest(new ErrorResponse("Account class must be between 1 and 7."));
            }

            var result = await service.ListAccountsAsync(
                search,
                accountClass,
                includeInactive == true,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        accounts.MapGet("/{code}", async (
            string code,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAccountAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        accounts.MapPost("", async (
            CreateChartAccountRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAccountAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/accounting/accounts/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        accounts.MapPut("/{code}", async (
            string code,
            UpdateChartAccountRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAccountAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        accounts.MapPost("/{code}/activate", async (
            string code,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetAccountActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        accounts.MapPost("/{code}/deactivate", async (
            string code,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetAccountActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);
    }

    private static void MapJournalEndpoints(RouteGroupBuilder api)
    {
        var journals = api.MapGroup("/accounting/journals")
            .WithTags("Accounting journals");

        journals.MapGet("", async (
            bool? includeInactive,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListJournalsAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        journals.MapGet("/{code}", async (
            string code,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetJournalAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        journals.MapPost("", async (
            CreateAccountingJournalRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateJournalAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/accounting/journals/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        journals.MapPut("/{code}", async (
            string code,
            UpdateAccountingJournalRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateJournalAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        journals.MapPost("/{code}/activate", async (
            string code,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetJournalActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);

        journals.MapPost("/{code}/deactivate", async (
            string code,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetJournalActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceChartManage);
    }

    private static void MapJournalEntryEndpoints(RouteGroupBuilder api)
    {
        var entries = api.MapGroup("/accounting/entries")
            .WithTags("Journal entries");

        entries.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? journalCode,
            string? status,
            string? accountCode,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseStatus(status, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListEntriesAsync(
                from,
                to,
                journalCode,
                parsedStatus,
                accountCode,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        entries.MapGet("/{id:guid}", async (
            Guid id,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetEntryAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);

        entries.MapPost("", async (
            CreateJournalEntryRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateEntryAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/accounting/entries/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceEntryManage);

        entries.MapPut("/{id:guid}/lines", async (
            Guid id,
            UpdateJournalEntryLinesRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateEntryLinesAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceEntryManage);

        entries.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelJournalEntryRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelEntryAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceEntryManage);

        entries.MapPost("/{id:guid}/post", async (
            Guid id,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.PostEntryAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceEntryPost);

        // Returns the NEW (reversing) entry, not the corrected one. Behind accounting.post
        // because a reversal posts an entry of its own.
        entries.MapPost("/{id:guid}/reverse", async (
            Guid id,
            ReverseJournalEntryRequest request,
            IAccountingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReverseEntryAsync(id, request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/accounting/entries/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceEntryReverse);
    }

    private static void MapTrialBalanceEndpoints(RouteGroupBuilder api)
    {
        var accounting = api.MapGroup("/accounting")
            .WithTags("Trial balance");

        accounting.MapGet("/trial-balance", async (
            DateOnly? from,
            DateOnly? to,
            IAccountingService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.GetTrialBalanceAsync(from, to, cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceAccountingRead);
    }

    private static bool TryParseStatus(string? status, out EntryStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<EntryStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Journal entry status must be Draft, Posted or Cancelled.";
        return false;
    }
}
