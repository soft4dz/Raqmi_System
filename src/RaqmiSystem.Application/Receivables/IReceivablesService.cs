using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// Receivables and collection: the aged balance, the trace of dunning actions, and the credit
/// risk carried by a customer.
///
/// This module creates NO financial data of its own. It reads the invoices produced by the
/// billing module and adds exactly one thing: a log of what a human did to get paid.
/// </summary>
public interface IReceivablesService
{
    /// <summary>
    /// Aged trial balance as of <paramref name="asOfDate"/>. Only Issued invoices dated on or
    /// before that date are counted: drafts are not receivables, paid invoices are settled, and
    /// cancelled ones never existed commercially. The returned payload states this scope and the
    /// aging basis in plain text so no reader has to assume them.
    /// </summary>
    Task<AgingBalanceResponse> GetAgingBalanceAsync(
        DateOnly asOfDate,
        string? customerCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReminderResponse>> ListRemindersAsync(
        string? customerCode,
        string? invoiceNumber,
        DateOnly? from,
        DateOnly? to,
        ReminderLevel? level,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReminderResponse>> GetReminderAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Files the trace of a dunning action already carried out. Nothing is sent by the system.
    /// </summary>
    Task<ApplicationResult<ReminderResponse>> CreateReminderAsync(
        CreateReminderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerRiskResponse>> GetCustomerRiskAsync(
        string customerCode,
        CancellationToken cancellationToken);
}
