using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Organization;

public sealed class HotelUnitService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IHotelUnitService
{
    public async Task<IReadOnlyCollection<HotelUnitResponse>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.HotelUnits.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(unit => unit.IsActive);
        }

        var units = await query
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArrayAsync(cancellationToken);

        return units.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<HotelUnitResponse>> GetAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<HotelUnitResponse>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.HotelUnits
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        return unit is null
            ? ApplicationResult<HotelUnitResponse>.NotFound("Hotel unit was not found.")
            : ApplicationResult<HotelUnitResponse>.Success(Map(unit));
    }

    public async Task<ApplicationResult<HotelUnitResponse>> CreateAsync(
        CreateHotelUnitRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        HotelUnit unit;

        try
        {
            unit = new HotelUnit(request.Code, request.Name, request.UnitType, request.DisplayOrder);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<HotelUnitResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.HotelUnits
            .AnyAsync(current => current.Code == unit.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<HotelUnitResponse>.Conflict("Hotel unit code already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        unit.MarkCreated(context.UserName, now);
        dbContext.HotelUnits.Add(unit);

        await WriteAuditAsync(
            "organization.hotel_unit.created",
            unit,
            context,
            new { unit.Code, unit.Name, UnitType = unit.UnitType.ToString(), unit.DisplayOrder },
            cancellationToken);

        return ApplicationResult<HotelUnitResponse>.Success(Map(unit));
    }

    public async Task<ApplicationResult<HotelUnitResponse>> UpdateAsync(
        string code,
        UpdateHotelUnitRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<HotelUnitResponse>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.HotelUnits
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<HotelUnitResponse>.NotFound("Hotel unit was not found.");
        }

        try
        {
            unit.UpdateDetails(request.Name, request.UnitType, request.DisplayOrder);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<HotelUnitResponse>.Validation(ex.Message);
        }

        unit.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "organization.hotel_unit.updated",
            unit,
            context,
            new { unit.Code, unit.Name, UnitType = unit.UnitType.ToString(), unit.DisplayOrder, unit.IsActive },
            cancellationToken);

        return ApplicationResult<HotelUnitResponse>.Success(Map(unit));
    }

    public async Task<ApplicationResult<HotelUnitResponse>> SetActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<HotelUnitResponse>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.HotelUnits
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<HotelUnitResponse>.NotFound("Hotel unit was not found.");
        }

        if (isActive)
        {
            unit.Activate();
        }
        else
        {
            unit.Deactivate();
        }

        unit.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "organization.hotel_unit.activated" : "organization.hotel_unit.deactivated",
            unit,
            context,
            new { unit.Code, unit.IsActive },
            cancellationToken);

        return ApplicationResult<HotelUnitResponse>.Success(Map(unit));
    }

    private static HotelUnitResponse Map(HotelUnit unit)
    {
        return new HotelUnitResponse(
            unit.Id,
            unit.Code,
            unit.Name,
            unit.UnitType,
            unit.DisplayOrder,
            unit.IsActive,
            unit.CreatedAt,
            unit.CreatedBy,
            unit.UpdatedAt,
            unit.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private async Task WriteAuditAsync(
        string action,
        HotelUnit unit,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                "organization.hotel_units",
                unit.Code,
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
