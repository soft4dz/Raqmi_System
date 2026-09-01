using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Api.Endpoints;

internal static class TreasuryEndpoints
{
    public static RouteGroupBuilder MapTreasuryEndpoints(this RouteGroupBuilder api)
    {
        MapBankAccountEndpoints(api);
        MapReceiptEndpoints(api);
        MapPaymentOrderEndpoints(api);

        return api;
    }

    private static void MapBankAccountEndpoints(RouteGroupBuilder api)
    {
        var accounts = api.MapGroup("/treasury/bank-accounts")
            .WithTags("Bank accounts");

        accounts.MapGet("", async (
            bool? includeInactive,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListBankAccountsAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        accounts.MapGet("/{code}", async (
            string code,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetBankAccountAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        accounts.MapPost("", async (
            CreateBankAccountRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBankAccountAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/treasury/bank-accounts/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBankAccountManage);

        accounts.MapPut("/{code}", async (
            string code,
            UpdateBankAccountRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateBankAccountAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBankAccountManage);

        accounts.MapPost("/{code}/activate", async (
            string code,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetBankAccountActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBankAccountManage);

        accounts.MapPost("/{code}/deactivate", async (
            string code,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetBankAccountActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceBankAccountManage);
    }

    private static void MapReceiptEndpoints(RouteGroupBuilder api)
    {
        var receipts = api.MapGroup("/treasury/receipts")
            .WithTags("Cash receipts");

        receipts.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? method,
            string? status,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseEnum<PaymentMethod>(method, MethodError, out var parsedMethod, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            if (!TryParseEnum<ReceiptStatus>(status, ReceiptStatusError, out var parsedStatus, out error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListReceiptsAsync(from, to, hotelUnitCode, parsedMethod, parsedStatus, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        receipts.MapGet("/summary", async (
            DateOnly? from,
            DateOnly? to,
            string? hotelUnitCode,
            string? status,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseEnum<ReceiptStatus>(status, ReceiptStatusError, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetReceiptSummaryAsync(from, to, hotelUnitCode, parsedStatus, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        receipts.MapGet("/{id:guid}", async (
            Guid id,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetReceiptAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        receipts.MapPost("", async (
            CreateCashReceiptRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateReceiptAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/treasury/receipts/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceiptManage);

        receipts.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCashReceiptRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateReceiptAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceiptManage);

        receipts.MapPost("/{id:guid}/confirm", async (
            Guid id,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmReceiptAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceiptManage);

        receipts.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelCashReceiptRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelReceiptAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceiptManage);
    }

    private static void MapPaymentOrderEndpoints(RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/treasury/payment-orders")
            .WithTags("Payment orders");

        orders.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? bankAccountCode,
            string? status,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseEnum<PaymentOrderStatus>(status, PaymentOrderStatusError, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListPaymentOrdersAsync(from, to, bankAccountCode, parsedStatus, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        orders.MapGet("/{id:guid}", async (
            Guid id,
            ITreasuryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetPaymentOrderAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceTreasuryRead);

        orders.MapPost("", async (
            CreatePaymentOrderRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePaymentOrderAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/treasury/payment-orders/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinancePaymentOrderManage);

        orders.MapPost("/{id:guid}/approve", async (
            Guid id,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApprovePaymentOrderAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinancePaymentOrderApprove);

        orders.MapPost("/{id:guid}/pay", async (
            Guid id,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.PayPaymentOrderAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinancePaymentOrderManage);

        orders.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelPaymentOrderRequest request,
            ITreasuryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelPaymentOrderAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinancePaymentOrderManage);
    }

    private const string MethodError = "Payment method must be Cash, Card, Cheque or BankTransfer.";
    private const string ReceiptStatusError = "Receipt status must be Draft, Confirmed or Cancelled.";
    private const string PaymentOrderStatusError = "Payment order status must be Draft, Approved, Paid or Cancelled.";

    private static bool TryParseEnum<TEnum>(
        string? value,
        string invalidMessage,
        out TEnum? parsed,
        out string error)
        where TEnum : struct, Enum
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var result) &&
            Enum.IsDefined(result))
        {
            parsed = result;
            return true;
        }

        error = invalidMessage;
        return false;
    }
}
