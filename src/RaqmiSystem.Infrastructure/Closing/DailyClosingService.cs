using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Closing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Closing;

/// <summary>
/// Daily closing (night audit) workflow: officially locks one business day for one hotel
/// unit. Entities are accessed through <c>dbContext.Set&lt;T&gt;()</c> so this service does
/// not depend on named DbSet properties of <see cref="RaqmiDbContext"/>.
/// Also implements <see cref="IDailyClosingReadService"/> so other modules can check the
/// lock without taking a dependency on the full closing workflow.
/// </summary>
public sealed class DailyClosingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IDailyClosingService, IDailyClosingReadService
{
    public async Task<IReadOnlyCollection<DailyClosingResponse>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<DailyClosing>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(closing => closing.BusinessDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(closing => closing.BusinessDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (!string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            query = query.Where(closing => closing.HotelUnitCode == normalizedUnitCode);
        }

        var rows = await query
            .GroupJoin(
                dbContext.Set<HotelUnit>().AsNoTracking(),
                closing => closing.HotelUnitCode,
                unit => unit.Code,
                (closing, units) => new { Closing = closing, UnitName = units.Select(unit => unit.Name).FirstOrDefault() })
            .OrderByDescending(row => row.Closing.BusinessDate)
            .ThenBy(row => row.Closing.HotelUnitCode)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => Map(row.Closing, row.UnitName)).ToArray();
    }

    public async Task<ApplicationResult<DailyClosingResponse>> GetAsync(
        DateOnly businessDate,
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<DailyClosingResponse>.Validation("Hotel unit code is required.");
        }

        var closing = await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.BusinessDate == businessDate && current.HotelUnitCode == normalizedUnitCode,
                cancellationToken);

        if (closing is null)
        {
            return ApplicationResult<DailyClosingResponse>.NotFound("Daily closing was not found for this date and hotel unit.");
        }

        return ApplicationResult<DailyClosingResponse>.Success(await MapWithUnitNameAsync(closing, cancellationToken));
    }

    public async Task<ApplicationResult<DailyClosingResponse>> CloseAsync(
        CloseBusinessDayRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<DailyClosingResponse>.Validation("Hotel unit code is required.");
        }

        // A business day that has not happened yet (UTC) cannot be officially closed.
        if (request.BusinessDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return ApplicationResult<DailyClosingResponse>.Validation("A business day in the future cannot be closed.");
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<DailyClosingResponse>.Validation("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<DailyClosingResponse>.Validation("The business day cannot be closed for an inactive hotel unit.");
        }

        // Check-then-insert below is not atomic on its own. A transaction with the provider's
        // default isolation level is used deliberately (the SQLite provider backing the
        // integration tests does not honour IsolationLevel.Serializable the way PostgreSQL
        // does), so two races remain conceivable and are each closed elsewhere:
        //   1. Two concurrent first closings of the same day/unit: both see existing == null;
        //      the unique index ix_daily_closings_business_date_hotel_unit_code rejects the
        //      loser, which is translated into a clean 409 Conflict in the catch below.
        //   2. A revenue entry created/submitted between the pending-revenue check and the
        //      commit (and, symmetrically, a closing inserted while a revenue write is in
        //      flight): this residual TOCTOU window is closed in practice by the symmetric
        //      guard in DailyRevenueService (every revenue create/update/status change
        //      re-checks IsClosedAsync) combined with the one-revenue-per-day/unit and
        //      one-closing-per-day/unit unique indexes.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existing = await dbContext.Set<DailyClosing>()
            .SingleOrDefaultAsync(
                current => current.BusinessDate == request.BusinessDate && current.HotelUnitCode == normalizedUnitCode,
                cancellationToken);

        if (existing is not null && existing.Status == ClosingStatus.Closed)
        {
            return ApplicationResult<DailyClosingResponse>.Conflict("The business day is already closed for this hotel unit.");
        }

        // A rejected revenue entry can be edited back to Draft while the day is reopened, so
        // the pending-revenue rule applies both to the first closing and to a re-closing.
        var hasPendingRevenues = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .AnyAsync(
                revenue => revenue.BusinessDate == request.BusinessDate
                    && revenue.HotelUnitCode == normalizedUnitCode
                    && (revenue.Status == DailyRevenueStatus.Draft || revenue.Status == DailyRevenueStatus.Submitted),
                cancellationToken);

        if (hasPendingRevenues)
        {
            return ApplicationResult<DailyClosingResponse>.Validation(
                "The business day cannot be closed while revenue entries are still Draft or Submitted.");
        }

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            // Reopened day: close it again, keeping the reopening trail on the entity.
            existing.CloseAgain(context.UserName, now);
            existing.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "exploitation.daily_closing.closed",
                existing,
                context,
                new { existing.BusinessDate, existing.HotelUnitCode, Status = existing.Status.ToString(), Reclosed = true },
                cancellationToken);

            // WriteAuditAsync already flushed via its internal SaveChangesAsync; this explicit
            // call is a deliberate no-op safeguard so persistence of the closing never depends
            // on the audit writer's internals.
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<DailyClosingResponse>.Success(Map(existing, unit.Name));
        }

        DailyClosing closing;

        try
        {
            closing = new DailyClosing(
                request.BusinessDate,
                normalizedUnitCode,
                context.UserName,
                now,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<DailyClosingResponse>.Validation(ex.Message);
        }

        closing.MarkCreated(context.UserName, now);
        dbContext.Set<DailyClosing>().Add(closing);

        try
        {
            await WriteAuditAsync(
                "exploitation.daily_closing.closed",
                closing,
                context,
                new { closing.BusinessDate, closing.HotelUnitCode, Status = closing.Status.ToString(), Reclosed = false },
                cancellationToken);

            // See the re-close branch above: explicit flush for clarity, no-op when the audit
            // writer already saved everything.
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Two concurrent first closings both saw existing == null; the unique index
            // ix_daily_closings_business_date_hotel_unit_code rejected this one.
            return ApplicationResult<DailyClosingResponse>.Conflict("Daily closing already exists for this date and hotel unit.");
        }

        return ApplicationResult<DailyClosingResponse>.Success(Map(closing, unit.Name));
    }

    public async Task<ApplicationResult<DailyClosingResponse>> ReopenAsync(
        Guid id,
        ReopenDailyClosingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var closing = await dbContext.Set<DailyClosing>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (closing is null)
        {
            return ApplicationResult<DailyClosingResponse>.NotFound("Daily closing was not found.");
        }

        try
        {
            closing.Reopen(request.Reason, context.UserName, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<DailyClosingResponse>.Validation(ex.Message);
        }

        closing.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "exploitation.daily_closing.reopened",
            closing,
            context,
            new { closing.BusinessDate, closing.HotelUnitCode, Status = closing.Status.ToString(), closing.ReopenReason },
            cancellationToken);

        // Explicit flush for clarity: the audit writer's internal SaveChangesAsync already
        // persisted the reopening, so this is a no-op safeguard.
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<DailyClosingResponse>.Success(await MapWithUnitNameAsync(closing, cancellationToken));
    }

    public async Task<bool> IsClosedAsync(
        DateOnly businessDate,
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is null)
        {
            return false;
        }

        return await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .AnyAsync(
                current => current.BusinessDate == businessDate
                    && current.HotelUnitCode == normalizedUnitCode
                    && current.Status == ClosingStatus.Closed,
                cancellationToken);
    }

    private async Task<DailyClosingResponse> MapWithUnitNameAsync(
        DailyClosing closing,
        CancellationToken cancellationToken)
    {
        var unitName = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.Code == closing.HotelUnitCode)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return Map(closing, unitName);
    }

    private static DailyClosingResponse Map(DailyClosing closing, string? unitName)
    {
        return new DailyClosingResponse(
            closing.Id,
            closing.BusinessDate,
            closing.HotelUnitCode,
            unitName,
            closing.Status,
            closing.ClosedAt,
            closing.ClosedBy,
            closing.ReopenedAt,
            closing.ReopenedBy,
            closing.ReopenReason,
            closing.Notes,
            closing.CreatedAt,
            closing.CreatedBy,
            closing.UpdatedAt,
            closing.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private async Task WriteAuditAsync(
        string action,
        DailyClosing closing,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                "exploitation.daily_closings",
                closing.Id.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
