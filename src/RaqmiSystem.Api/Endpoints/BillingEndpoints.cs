using RaqmiSystem.Application.Billing;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

internal static class BillingEndpoints
{

    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder api)
    {
        MapCustomerEndpoints(api);
        MapInvoiceEndpoints(api);
        return api;
    }

    private static void MapCustomerEndpoints(RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/billing/customers")
            .WithTags("Customers");

        customers.MapGet("", async (
            string? search,
            bool? includeInactive,
            IBillingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListCustomersAsync(search, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.CustomersRead);

        customers.MapGet("/{code}", async (
            string code,
            IBillingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomerAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CustomersRead);

        customers.MapPost("", async (
            CreateCustomerRequest request,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCustomerAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/billing/customers/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CustomersWrite);

        customers.MapPut("/{code}", async (
            string code,
            UpdateCustomerRequest request,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCustomerAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CustomersWrite);

        customers.MapPost("/{code}/activate", async (
            string code,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCustomerActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CustomersWrite);

        customers.MapPost("/{code}/deactivate", async (
            string code,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCustomerActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.CustomersWrite);
    }

    private static void MapInvoiceEndpoints(RouteGroupBuilder api)
    {
        var invoices = api.MapGroup("/billing/invoices")
            .WithTags("Invoices");

        invoices.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? customerCode,
            string? hotelUnitCode,
            string? status,
            IBillingService service,
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

            var result = await service.ListInvoicesAsync(
                from,
                to,
                customerCode,
                hotelUnitCode,
                parsedStatus,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.InvoicesRead);

        invoices.MapGet("/{id:guid}", async (
            Guid id,
            IBillingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetInvoiceAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesRead);

        invoices.MapPost("", async (
            CreateInvoiceRequest request,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateInvoiceAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/billing/invoices/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesWrite);

        invoices.MapPut("/{id:guid}/lines", async (
            Guid id,
            UpdateInvoiceLinesRequest request,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateInvoiceLinesAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesWrite);

        invoices.MapPost("/{id:guid}/issue", async (
            Guid id,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.IssueInvoiceAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesIssue);

        invoices.MapPost("/{id:guid}/pay", async (
            Guid id,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.MarkInvoicePaidAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesWrite);

        invoices.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelInvoiceRequest request,
            IBillingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelInvoiceAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.InvoicesWrite);
    }

    private static bool TryParseStatus(string? status, out InvoiceStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<InvoiceStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Invoice status must be Draft, Issued, Paid or Cancelled.";
        return false;
    }
}
