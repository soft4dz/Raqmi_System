using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Billing;

public interface IBillingService
{
    Task<IReadOnlyCollection<CustomerResponse>> ListCustomersAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerResponse>> GetCustomerAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerResponse>> CreateCustomerAsync(
        CreateCustomerRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerResponse>> UpdateCustomerAsync(
        string code,
        UpdateCustomerRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerResponse>> SetCustomerActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InvoiceResponse>> ListInvoicesAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        InvoiceStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> GetInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> UpdateInvoiceLinesAsync(
        Guid id,
        UpdateInvoiceLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> IssueInvoiceAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> MarkInvoicePaidAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InvoiceResponse>> CancelInvoiceAsync(
        Guid id,
        CancelInvoiceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
