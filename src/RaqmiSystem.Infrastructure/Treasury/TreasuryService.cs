using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Treasury;

public sealed class TreasuryService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IApprovalGate approvalGate) : ITreasuryService
{
    private const string BankAccountEntityName = "finance.bank_accounts";
    private const string CashReceiptEntityName = "finance.cash_receipts";
    private const string PaymentOrderEntityName = "finance.payment_orders";

    public async Task<IReadOnlyCollection<BankAccountResponse>> ListBankAccountsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<BankAccount>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(account => account.IsActive);
        }

        var accounts = await query
            .OrderBy(account => account.Code)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<BankAccountResponse>> GetBankAccountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var account = await dbContext.Set<BankAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<BankAccountResponse>.NotFound("Bank account was not found.");
        }

        return ApplicationResult<BankAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<BankAccountResponse>> CreateBankAccountAsync(
        CreateBankAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        BankAccount account;

        try
        {
            account = new BankAccount(request.Code, request.Label, request.BankName, request.AccountNumber);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<BankAccountResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<BankAccount>()
            .AnyAsync(current => current.Code == account.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<BankAccountResponse>.Conflict("A bank account with this code already exists.");
        }

        account.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<BankAccount>().Add(account);

        try
        {
            await WriteAuditAsync(
                "finance.bank_account.created",
                BankAccountEntityName,
                account.Id,
                context,
                new { account.Code, account.Label, account.BankName },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same code loses the race against the unique index on bank_accounts.code.
            return ApplicationResult<BankAccountResponse>.Conflict("A bank account with this code already exists.");
        }

        return ApplicationResult<BankAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<BankAccountResponse>> UpdateBankAccountAsync(
        string code,
        UpdateBankAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var account = await dbContext.Set<BankAccount>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<BankAccountResponse>.NotFound("Bank account was not found.");
        }

        try
        {
            account.UpdateDetails(request.Label, request.BankName, request.AccountNumber);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<BankAccountResponse>.Validation(ex.Message);
        }

        account.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "finance.bank_account.updated",
            BankAccountEntityName,
            account.Id,
            context,
            new { account.Code, account.Label, account.BankName },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<BankAccountResponse>.Success(Map(account));
    }

    public async Task<ApplicationResult<BankAccountResponse>> SetBankAccountActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var account = await dbContext.Set<BankAccount>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<BankAccountResponse>.NotFound("Bank account was not found.");
        }

        if (isActive)
        {
            account.Activate();
        }
        else
        {
            account.Deactivate();
        }

        account.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "finance.bank_account.activated" : "finance.bank_account.deactivated",
            BankAccountEntityName,
            account.Id,
            context,
            new { account.Code, account.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<BankAccountResponse>.Success(Map(account));
    }

    public async Task<IReadOnlyCollection<CashReceiptResponse>> ListReceiptsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        PaymentMethod? method,
        ReceiptStatus? status,
        CancellationToken cancellationToken)
    {
        var query = ApplyReceiptFilters(
            dbContext.Set<CashReceipt>().AsNoTracking(),
            from,
            to,
            hotelUnitCode,
            method,
            status);

        var rows = await query
            .GroupJoin(
                dbContext.Set<HotelUnit>().AsNoTracking(),
                receipt => receipt.HotelUnitCode,
                unit => unit.Code,
                (receipt, units) => new { Receipt = receipt, UnitName = units.Select(unit => unit.Name).FirstOrDefault() })
            .OrderByDescending(row => row.Receipt.ReceiptDate)
            .ThenBy(row => row.Receipt.HotelUnitCode)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => Map(row.Receipt, row.UnitName)).ToArray();
    }

    public async Task<ApplicationResult<CashReceiptResponse>> GetReceiptAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Set<CashReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (receipt is null)
        {
            return ApplicationResult<CashReceiptResponse>.NotFound("Cash receipt was not found.");
        }

        return ApplicationResult<CashReceiptResponse>.Success(
            await MapWithUnitNameAsync(receipt, cancellationToken));
    }

    public async Task<ApplicationResult<CashReceiptResponse>> CreateReceiptAsync(
        CreateCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitCheck = await CheckHotelUnitAsync<CashReceiptResponse>(request.HotelUnitCode, cancellationToken);

        if (unitCheck is not null)
        {
            return unitCheck;
        }

        var accountCheck = await CheckOptionalBankAccountAsync<CashReceiptResponse>(request.BankAccountCode, cancellationToken);

        if (accountCheck is not null)
        {
            return accountCheck;
        }

        CashReceipt receipt;

        try
        {
            receipt = new CashReceipt(
                request.ReceiptDate,
                request.HotelUnitCode,
                request.Method,
                request.Amount,
                request.Reference,
                request.BankAccountCode,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CashReceiptResponse>.Validation(ex.Message);
        }

        receipt.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<CashReceipt>().Add(receipt);

        await WriteAuditAsync(
            "finance.cash_receipt.created",
            CashReceiptEntityName,
            receipt.Id,
            context,
            new { receipt.ReceiptDate, receipt.HotelUnitCode, Method = receipt.Method.ToString(), receipt.Amount },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CashReceiptResponse>.Success(
            await MapWithUnitNameAsync(receipt, cancellationToken));
    }

    public async Task<ApplicationResult<CashReceiptResponse>> UpdateReceiptAsync(
        Guid id,
        UpdateCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Set<CashReceipt>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (receipt is null)
        {
            return ApplicationResult<CashReceiptResponse>.NotFound("Cash receipt was not found.");
        }

        if (!receipt.CanEdit)
        {
            return ApplicationResult<CashReceiptResponse>.Validation("Only draft receipts can be edited.");
        }

        var unitCheck = await CheckHotelUnitAsync<CashReceiptResponse>(request.HotelUnitCode, cancellationToken);

        if (unitCheck is not null)
        {
            return unitCheck;
        }

        var accountCheck = await CheckOptionalBankAccountAsync<CashReceiptResponse>(request.BankAccountCode, cancellationToken);

        if (accountCheck is not null)
        {
            return accountCheck;
        }

        try
        {
            receipt.Update(
                request.ReceiptDate,
                request.HotelUnitCode,
                request.Method,
                request.Amount,
                request.Reference,
                request.BankAccountCode,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<CashReceiptResponse>.Validation(ex.Message);
        }

        receipt.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "finance.cash_receipt.updated",
            CashReceiptEntityName,
            receipt.Id,
            context,
            new { receipt.ReceiptDate, receipt.HotelUnitCode, Method = receipt.Method.ToString(), receipt.Amount },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CashReceiptResponse>.Success(
            await MapWithUnitNameAsync(receipt, cancellationToken));
    }

    public async Task<ApplicationResult<CashReceiptResponse>> ConfirmReceiptAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeReceiptStatusAsync(
            id,
            context,
            "finance.cash_receipt.confirmed",
            receipt => receipt.Confirm(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<CashReceiptResponse>> CancelReceiptAsync(
        Guid id,
        CancelCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeReceiptStatusAsync(
            id,
            context,
            "finance.cash_receipt.cancelled",
            receipt => receipt.Cancel(request.Reason, context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<CashReceiptSummaryResponse>> GetReceiptSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReceiptStatus? status,
        CancellationToken cancellationToken)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ApplicationResult<CashReceiptSummaryResponse>.Validation("The from date cannot be after the to date.");
        }

        // Business rule: without an explicit status filter, the summary only counts Confirmed
        // receipts - it is a total of actual collected money, so Draft (not yet real) and
        // Cancelled (reversed) receipts must not inflate it. An explicit status filter is
        // honoured as-is, which lets callers inspect drafts or cancellations deliberately.
        var effectiveStatus = status ?? ReceiptStatus.Confirmed;

        var rows = await ApplyReceiptFilters(
                dbContext.Set<CashReceipt>().AsNoTracking(),
                from,
                to,
                hotelUnitCode,
                method: null,
                effectiveStatus)
            .ToArrayAsync(cancellationToken);

        var cash = rows.Where(row => row.Method == PaymentMethod.Cash).Sum(row => row.Amount);
        var card = rows.Where(row => row.Method == PaymentMethod.Card).Sum(row => row.Amount);
        var cheque = rows.Where(row => row.Method == PaymentMethod.Cheque).Sum(row => row.Amount);
        var bankTransfer = rows.Where(row => row.Method == PaymentMethod.BankTransfer).Sum(row => row.Amount);

        var summary = new CashReceiptSummaryResponse(
            from,
            to,
            NormalizeNullableCode(hotelUnitCode),
            effectiveStatus,
            rows.Length,
            rows.Count(row => row.Status == ReceiptStatus.Draft),
            rows.Count(row => row.Status == ReceiptStatus.Confirmed),
            rows.Count(row => row.Status == ReceiptStatus.Cancelled),
            cash,
            card,
            cheque,
            bankTransfer,
            cash + card + cheque + bankTransfer);

        return ApplicationResult<CashReceiptSummaryResponse>.Success(summary);
    }

    public async Task<IReadOnlyCollection<PaymentOrderResponse>> ListPaymentOrdersAsync(
        DateOnly? from,
        DateOnly? to,
        string? bankAccountCode,
        PaymentOrderStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<PaymentOrder>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(order => order.OrderDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(order => order.OrderDate <= to.Value);
        }

        var normalizedAccountCode = NormalizeNullableCode(bankAccountCode);

        if (!string.IsNullOrWhiteSpace(normalizedAccountCode))
        {
            query = query.Where(order => order.BankAccountCode == normalizedAccountCode);
        }

        if (status.HasValue)
        {
            query = query.Where(order => order.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenBy(order => order.DueDate)
            .ToArrayAsync(cancellationToken);

        return orders.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<PaymentOrderResponse>> GetPaymentOrderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Set<PaymentOrder>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (order is null)
        {
            return ApplicationResult<PaymentOrderResponse>.NotFound("Payment order was not found.");
        }

        return ApplicationResult<PaymentOrderResponse>.Success(Map(order));
    }

    public async Task<ApplicationResult<PaymentOrderResponse>> CreatePaymentOrderAsync(
        CreatePaymentOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedAccountCode = NormalizeCodeOrEmpty(request.BankAccountCode);

        if (string.IsNullOrWhiteSpace(normalizedAccountCode))
        {
            return ApplicationResult<PaymentOrderResponse>.Validation("Bank account code is required.");
        }

        var account = await dbContext.Set<BankAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedAccountCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<PaymentOrderResponse>.NotFound("Bank account was not found.");
        }

        if (!account.IsActive)
        {
            return ApplicationResult<PaymentOrderResponse>.Validation("A payment order cannot use an inactive bank account.");
        }

        PaymentOrder order;

        try
        {
            order = new PaymentOrder(
                request.OrderDate,
                request.Beneficiary,
                request.Amount,
                request.DueDate,
                request.BankAccountCode,
                request.Reference);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PaymentOrderResponse>.Validation(ex.Message);
        }

        order.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<PaymentOrder>().Add(order);

        await WriteAuditAsync(
            "finance.payment_order.created",
            PaymentOrderEntityName,
            order.Id,
            context,
            new { order.OrderDate, order.Beneficiary, order.Amount, order.DueDate, order.BankAccountCode },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<PaymentOrderResponse>.Success(Map(order));
    }

    /// <summary>
    /// Approving a payment order is subject to the approvals module's gate. The gate is
    /// backward-compatible: with NO active circuit for payment orders it always clears, so an
    /// installation that never configured a circuit approves exactly as before. Once a circuit is
    /// activated, only an order carrying an APPROVED approval instance may be approved here - an
    /// order with no instance, one still in progress or a rejected one is refused.
    ///
    /// The gate runs as a guard INSIDE the status change, i.e. after the order was loaded: an
    /// unknown id keeps answering NotFound rather than being masked by a gate refusal.
    /// </summary>
    public async Task<ApplicationResult<PaymentOrderResponse>> ApprovePaymentOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangePaymentOrderStatusAsync(
            id,
            context,
            "finance.payment_order.approved",
            order => order.Approve(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken,
            guard: CheckApprovalCircuitAsync);
    }

    /// <summary>
    /// Asks the approvals gate whether this payment order may proceed. A refusal from the gate
    /// itself (a malformed reference, for instance) is propagated rather than reinterpreted.
    /// </summary>
    private async Task<ApplicationResult<PaymentOrderResponse>?> CheckApprovalCircuitAsync(
        PaymentOrder order,
        CancellationToken cancellationToken)
    {
        var gateResult = await approvalGate.IsApprovedAsync(
            ApprovalSubjectType.PaymentOrder,
            order.Id.ToString(),
            cancellationToken);

        if (!gateResult.Succeeded)
        {
            return ApplicationResult<PaymentOrderResponse>.Validation(
                gateResult.Error ?? "The approvals module refused to evaluate this payment order.");
        }

        if (!gateResult.Value)
        {
            return ApplicationResult<PaymentOrderResponse>.Validation(
                "This payment order must first clear its validation circuit: an approval request " +
                "has to be opened and approved in the Validations module before the order can be " +
                "approved here.");
        }

        return null;
    }

    public async Task<ApplicationResult<PaymentOrderResponse>> PayPaymentOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangePaymentOrderStatusAsync(
            id,
            context,
            "finance.payment_order.paid",
            order => order.MarkPaid(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<PaymentOrderResponse>> CancelPaymentOrderAsync(
        Guid id,
        CancelPaymentOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangePaymentOrderStatusAsync(
            id,
            context,
            "finance.payment_order.cancelled",
            order => order.Cancel(request.Reason, context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private async Task<ApplicationResult<CashReceiptResponse>> ChangeReceiptStatusAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<CashReceipt> change,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Set<CashReceipt>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (receipt is null)
        {
            return ApplicationResult<CashReceiptResponse>.NotFound("Cash receipt was not found.");
        }

        try
        {
            change(receipt);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<CashReceiptResponse>.Validation(ex.Message);
        }

        receipt.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            auditAction,
            CashReceiptEntityName,
            receipt.Id,
            context,
            new { receipt.ReceiptDate, receipt.HotelUnitCode, receipt.Amount, Status = receipt.Status.ToString(), receipt.CancelReason },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CashReceiptResponse>.Success(
            await MapWithUnitNameAsync(receipt, cancellationToken));
    }

    private async Task<ApplicationResult<PaymentOrderResponse>> ChangePaymentOrderStatusAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<PaymentOrder> change,
        CancellationToken cancellationToken,
        Func<PaymentOrder, CancellationToken, Task<ApplicationResult<PaymentOrderResponse>?>>? guard = null)
    {
        var order = await dbContext.Set<PaymentOrder>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (order is null)
        {
            return ApplicationResult<PaymentOrderResponse>.NotFound("Payment order was not found.");
        }

        // An optional cross-module precondition, evaluated once the order exists and before any
        // mutation: it returns a refusal to propagate, or null to let the change proceed.
        if (guard is not null)
        {
            var refusal = await guard(order, cancellationToken);

            if (refusal is not null)
            {
                return refusal;
            }
        }

        try
        {
            change(order);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<PaymentOrderResponse>.Validation(ex.Message);
        }

        order.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            auditAction,
            PaymentOrderEntityName,
            order.Id,
            context,
            new { order.OrderDate, order.Beneficiary, order.Amount, Status = order.Status.ToString(), order.CancelReason },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<PaymentOrderResponse>.Success(Map(order));
    }

    private async Task<ApplicationResult<T>?> CheckHotelUnitAsync<T>(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<T>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<T>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<T>.Validation("A receipt cannot be recorded for an inactive hotel unit.");
        }

        return null;
    }

    private async Task<ApplicationResult<T>?> CheckOptionalBankAccountAsync<T>(
        string? bankAccountCode,
        CancellationToken cancellationToken)
    {
        var normalizedAccountCode = NormalizeNullableCode(bankAccountCode);

        if (normalizedAccountCode is null)
        {
            return null;
        }

        var account = await dbContext.Set<BankAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedAccountCode, cancellationToken);

        if (account is null)
        {
            return ApplicationResult<T>.NotFound("Bank account was not found.");
        }

        if (!account.IsActive)
        {
            return ApplicationResult<T>.Validation("A receipt cannot use an inactive bank account.");
        }

        return null;
    }

    private static IQueryable<CashReceipt> ApplyReceiptFilters(
        IQueryable<CashReceipt> query,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        PaymentMethod? method,
        ReceiptStatus? status)
    {
        if (from.HasValue)
        {
            query = query.Where(receipt => receipt.ReceiptDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(receipt => receipt.ReceiptDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (!string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            query = query.Where(receipt => receipt.HotelUnitCode == normalizedUnitCode);
        }

        if (method.HasValue)
        {
            query = query.Where(receipt => receipt.Method == method.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(receipt => receipt.Status == status.Value);
        }

        return query;
    }

    private async Task<CashReceiptResponse> MapWithUnitNameAsync(
        CashReceipt receipt,
        CancellationToken cancellationToken)
    {
        var unitName = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.Code == receipt.HotelUnitCode)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return Map(receipt, unitName);
    }

    private static BankAccountResponse Map(BankAccount account)
    {
        return new BankAccountResponse(
            account.Id,
            account.Code,
            account.Label,
            account.BankName,
            account.AccountNumber,
            account.IsActive,
            account.CreatedAt,
            account.CreatedBy,
            account.UpdatedAt,
            account.UpdatedBy);
    }

    private static CashReceiptResponse Map(CashReceipt receipt, string? unitName)
    {
        return new CashReceiptResponse(
            receipt.Id,
            receipt.ReceiptDate,
            receipt.HotelUnitCode,
            unitName,
            receipt.Method,
            receipt.Amount,
            receipt.Reference,
            receipt.BankAccountCode,
            receipt.Notes,
            receipt.Status,
            receipt.CanEdit,
            receipt.ConfirmedAt,
            receipt.ConfirmedBy,
            receipt.CancelledAt,
            receipt.CancelledBy,
            receipt.CancelReason,
            receipt.CreatedAt,
            receipt.CreatedBy,
            receipt.UpdatedAt,
            receipt.UpdatedBy);
    }

    private static PaymentOrderResponse Map(PaymentOrder order)
    {
        return new PaymentOrderResponse(
            order.Id,
            order.OrderDate,
            order.Beneficiary,
            order.Amount,
            order.DueDate,
            order.BankAccountCode,
            order.Reference,
            order.Status,
            order.ApprovedAt,
            order.ApprovedBy,
            order.PaidAt,
            order.PaidBy,
            order.CancelledAt,
            order.CancelledBy,
            order.CancelReason,
            order.CreatedAt,
            order.CreatedBy,
            order.UpdatedAt,
            order.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the
    /// audit row), so this call is usually a no-op - it exists so persistence never silently
    /// depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
