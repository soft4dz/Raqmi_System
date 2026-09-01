using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Receivables and collection: aged balance, dunning trace and customer risk.
///
/// Read operations are guarded by "receivables.read" and the single write operation by
/// "receivables.write". Recording a reminder is a write because it asserts, in the audit trail,
/// that somebody actually chased the customer - the system itself never contacts anyone.
/// </summary>
internal static class ReceivablesEndpoints
{
    public static RouteGroupBuilder MapReceivablesEndpoints(this RouteGroupBuilder api)
    {
        MapAgingEndpoints(api);
        MapReminderEndpoints(api);
        MapCustomerRiskEndpoints(api);

        return api;
    }

    private static void MapAgingEndpoints(RouteGroupBuilder api)
    {
        var receivables = api.MapGroup("/receivables")
            .WithTags("Receivables");

        receivables.MapGet("/aging", async (
            DateOnly? asOfDate,
            string? customerCode,
            IReceivablesService service,
            CancellationToken cancellationToken) =>
        {
            // No as-of date means "today": the aged balance is overwhelmingly read for the
            // current day, and forcing the caller to compute the date would only invite
            // client-side clock drift.
            var reportDate = asOfDate ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

            var result = await service.GetAgingBalanceAsync(reportDate, customerCode, cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceReceivableRead);
    }

    private static void MapReminderEndpoints(RouteGroupBuilder api)
    {
        var reminders = api.MapGroup("/receivables/reminders")
            .WithTags("Receivables reminders");

        reminders.MapGet("", async (
            string? customerCode,
            string? invoiceNumber,
            DateOnly? from,
            DateOnly? to,
            string? level,
            IReceivablesService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseLevel(level, out var parsedLevel, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListRemindersAsync(
                customerCode,
                invoiceNumber,
                from,
                to,
                parsedLevel,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.FinanceReceivableRead);

        reminders.MapGet("/{id:guid}", async (
            Guid id,
            IReceivablesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetReminderAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceivableRead);

        reminders.MapPost("", async (
            CreateReminderRequest request,
            IReceivablesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateReminderAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/receivables/reminders/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceivableRemind);
    }

    private static void MapCustomerRiskEndpoints(RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/receivables/customers")
            .WithTags("Receivables customer risk");

        customers.MapGet("/{code}/risk", async (
            string code,
            IReceivablesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomerRiskAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceReceivableRead);
    }

    private static bool TryParseLevel(string? level, out ReminderLevel? parsedLevel, out string error)
    {
        parsedLevel = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(level))
        {
            return true;
        }

        if (Enum.TryParse<ReminderLevel>(level.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedLevel = value;
            return true;
        }

        error = "Reminder level must be First, Second or FormalNotice.";
        return false;
    }
}
