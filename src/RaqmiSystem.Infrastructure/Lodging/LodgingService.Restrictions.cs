using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Regles de vente (stop sell, CTA, CTD, durees, delais) et autorisations de surreservation.
/// </summary>
public sealed partial class LodgingService
{
    // ==================================== Restrictions ====================================

    public async Task<ApplicationResult<IReadOnlyCollection<RateRestrictionResponse>>> ListRestrictionsAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<RateRestrictionResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<RateRestriction>()
            .AsNoTracking()
            .Where(restriction => restriction.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(restriction => restriction.IsActive);
        }

        if (from is { } start)
        {
            query = query.Where(restriction => restriction.ToDate >= start);
        }

        if (to is { } end)
        {
            query = query.Where(restriction => restriction.FromDate <= end);
        }

        var restrictions = await query
            .OrderBy(restriction => restriction.FromDate)
            .ThenBy(restriction => restriction.RoomTypeCode)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<RateRestrictionResponse>>.Success(
            restrictions.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<RateRestrictionResponse>> CreateRestrictionAsync(
        SaveRateRestrictionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RateRestrictionResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        RateRestriction restriction;

        try
        {
            restriction = new RateRestriction(
                unitFailure.UnitCode,
                request.FromDate,
                request.ToDate,
                request.RoomTypeCode,
                request.RatePlanCode,
                request.ChannelCode);

            restriction.SetRules(
                request.IsClosed,
                request.IsClosedToArrival,
                request.IsClosedToDeparture,
                request.MinimumStay,
                request.MaximumStay,
                request.MinAdvanceDays,
                request.MaxAdvanceDays);

            restriction.SetNotes(request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RateRestrictionResponse>.Validation(ex.Message);
        }

        if (restriction.IsEmpty)
        {
            return ApplicationResult<RateRestrictionResponse>.Validation(
                "Cette regle ne restreint rien : elle n'aurait aucun effet. Cochez au moins une "
                + "fermeture ou renseignez une duree.");
        }

        var typeFailure = await RequireRestrictionScopeAsync(restriction, cancellationToken);

        if (typeFailure is not null)
        {
            return typeFailure;
        }

        restriction.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<RateRestriction>().Add(restriction);

        await WriteAuditAsync(
            "lodging.restriction.created",
            RestrictionsEntity,
            restriction.Id,
            context,
            DescribeRestriction(restriction),
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RateRestrictionResponse>.Success(Map(restriction));
    }

    public async Task<ApplicationResult<RateRestrictionResponse>> UpdateRestrictionAsync(
        Guid id,
        SaveRateRestrictionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var restriction = await dbContext.Set<RateRestriction>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (restriction is null)
        {
            return ApplicationResult<RateRestrictionResponse>.NotFound("La regle de vente est introuvable.");
        }

        try
        {
            restriction.Reschedule(request.FromDate, request.ToDate);
            restriction.SetScope(request.RoomTypeCode, request.RatePlanCode, request.ChannelCode);
            restriction.SetRules(
                request.IsClosed,
                request.IsClosedToArrival,
                request.IsClosedToDeparture,
                request.MinimumStay,
                request.MaximumStay,
                request.MinAdvanceDays,
                request.MaxAdvanceDays);
            restriction.SetNotes(request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RateRestrictionResponse>.Validation(ex.Message);
        }

        if (restriction.IsEmpty)
        {
            return ApplicationResult<RateRestrictionResponse>.Validation(
                "Cette regle ne restreint rien : elle n'aurait aucun effet.");
        }

        var typeFailure = await RequireRestrictionScopeAsync(restriction, cancellationToken);

        if (typeFailure is not null)
        {
            return typeFailure;
        }

        restriction.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.restriction.updated",
            RestrictionsEntity,
            restriction.Id,
            context,
            DescribeRestriction(restriction),
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RateRestrictionResponse>.Success(Map(restriction));
    }

    public async Task<ApplicationResult<RateRestrictionResponse>> SetRestrictionActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var restriction = await dbContext.Set<RateRestriction>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (restriction is null)
        {
            return ApplicationResult<RateRestrictionResponse>.NotFound("La regle de vente est introuvable.");
        }

        if (isActive)
        {
            restriction.Activate();
        }
        else
        {
            restriction.Deactivate();
        }

        restriction.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.restriction.activated" : "lodging.restriction.deactivated",
            RestrictionsEntity,
            restriction.Id,
            context,
            DescribeRestriction(restriction),
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RateRestrictionResponse>.Success(Map(restriction));
    }

    /// <summary>
    /// Une regle qui cible un type INEXISTANT ne ferait jamais match : elle donnerait l'illusion
    /// d'une fermeture posee alors que la vente reste ouverte. C'est le genre d'erreur qu'on ne
    /// decouvre qu'en survendant, donc elle est refusee a la saisie.
    /// </summary>
    private async Task<ApplicationResult<RateRestrictionResponse>?> RequireRestrictionScopeAsync(
        RateRestriction restriction,
        CancellationToken cancellationToken)
    {
        if (restriction.RoomTypeCode is not { } roomTypeCode)
        {
            return null;
        }

        var exists = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .AnyAsync(
                type => type.HotelUnitCode == restriction.HotelUnitCode && type.Code == roomTypeCode,
                cancellationToken);

        return exists
            ? null
            : ApplicationResult<RateRestrictionResponse>.NotFound(
                $"Le type de chambre '{roomTypeCode}' n'existe pas dans l'unite "
                + $"'{restriction.HotelUnitCode}' : la regle ne s'appliquerait jamais.");
    }

    private static object DescribeRestriction(RateRestriction restriction)
    {
        return new
        {
            restriction.HotelUnitCode,
            restriction.RoomTypeCode,
            restriction.RatePlanCode,
            restriction.ChannelCode,
            restriction.FromDate,
            restriction.ToDate,
            restriction.IsClosed,
            restriction.IsClosedToArrival,
            restriction.IsClosedToDeparture,
            restriction.MinimumStay,
            restriction.MaximumStay,
            restriction.MinAdvanceDays,
            restriction.MaxAdvanceDays,
            restriction.IsActive
        };
    }

    private static RateRestrictionResponse Map(RateRestriction restriction)
    {
        return new RateRestrictionResponse(
            restriction.Id,
            restriction.HotelUnitCode,
            restriction.RoomTypeCode,
            restriction.RatePlanCode,
            restriction.ChannelCode,
            restriction.FromDate,
            restriction.ToDate,
            restriction.IsClosed,
            restriction.IsClosedToArrival,
            restriction.IsClosedToDeparture,
            restriction.MinimumStay,
            restriction.MaximumStay,
            restriction.MinAdvanceDays,
            restriction.MaxAdvanceDays,
            restriction.IsActive,
            restriction.Notes,
            restriction.CreatedAt,
            restriction.CreatedBy,
            restriction.UpdatedAt,
            restriction.UpdatedBy);
    }

    // =================================== Surreservation ===================================

    public async Task<ApplicationResult<IReadOnlyCollection<OverbookingAllowanceResponse>>> ListOverbookingAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<OverbookingAllowanceResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<OverbookingAllowance>()
            .AsNoTracking()
            .Where(allowance => allowance.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(allowance => allowance.IsActive);
        }

        if (from is { } start)
        {
            query = query.Where(allowance => allowance.ToDate >= start);
        }

        if (to is { } end)
        {
            query = query.Where(allowance => allowance.FromDate <= end);
        }

        var allowances = await query
            .OrderBy(allowance => allowance.FromDate)
            .ThenBy(allowance => allowance.RoomTypeCode)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<OverbookingAllowanceResponse>>.Success(
            allowances.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<OverbookingAllowanceResponse>> CreateOverbookingAsync(
        SaveOverbookingAllowanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<OverbookingAllowanceResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        OverbookingAllowance allowance;

        try
        {
            allowance = new OverbookingAllowance(
                unitFailure.UnitCode,
                request.RoomTypeCode,
                request.FromDate,
                request.ToDate,
                request.ExtraRooms,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<OverbookingAllowanceResponse>.Validation(ex.Message);
        }

        var typeFailure = await RequireActiveRoomTypeAsync<OverbookingAllowanceResponse>(
            allowance.HotelUnitCode,
            allowance.RoomTypeCode,
            cancellationToken);

        if (typeFailure is not null)
        {
            return typeFailure;
        }

        var overlapFailure = await RequireNoOverlappingAllowanceAsync(allowance, cancellationToken);

        if (overlapFailure is not null)
        {
            return overlapFailure;
        }

        allowance.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<OverbookingAllowance>().Add(allowance);

        await WriteAuditAsync(
            "lodging.overbooking.created",
            OverbookingEntity,
            allowance.Id,
            context,
            new
            {
                allowance.HotelUnitCode,
                allowance.RoomTypeCode,
                allowance.FromDate,
                allowance.ToDate,
                allowance.ExtraRooms
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<OverbookingAllowanceResponse>.Success(Map(allowance));
    }

    public async Task<ApplicationResult<OverbookingAllowanceResponse>> UpdateOverbookingAsync(
        Guid id,
        SaveOverbookingAllowanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allowance = await dbContext.Set<OverbookingAllowance>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (allowance is null)
        {
            return ApplicationResult<OverbookingAllowanceResponse>.NotFound(
                "L'autorisation de surreservation est introuvable.");
        }

        try
        {
            allowance.UpdateTerms(request.FromDate, request.ToDate, request.ExtraRooms, request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<OverbookingAllowanceResponse>.Validation(ex.Message);
        }

        var overlapFailure = await RequireNoOverlappingAllowanceAsync(allowance, cancellationToken);

        if (overlapFailure is not null)
        {
            return overlapFailure;
        }

        allowance.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.overbooking.updated",
            OverbookingEntity,
            allowance.Id,
            context,
            new
            {
                allowance.HotelUnitCode,
                allowance.RoomTypeCode,
                allowance.FromDate,
                allowance.ToDate,
                allowance.ExtraRooms
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<OverbookingAllowanceResponse>.Success(Map(allowance));
    }

    public async Task<ApplicationResult<OverbookingAllowanceResponse>> SetOverbookingActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var allowance = await dbContext.Set<OverbookingAllowance>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (allowance is null)
        {
            return ApplicationResult<OverbookingAllowanceResponse>.NotFound(
                "L'autorisation de surreservation est introuvable.");
        }

        if (isActive)
        {
            allowance.Activate();

            var overlapFailure = await RequireNoOverlappingAllowanceAsync(allowance, cancellationToken);

            if (overlapFailure is not null)
            {
                return overlapFailure;
            }
        }
        else
        {
            allowance.Deactivate();
        }

        allowance.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.overbooking.activated" : "lodging.overbooking.deactivated",
            OverbookingEntity,
            allowance.Id,
            context,
            new { allowance.HotelUnitCode, allowance.RoomTypeCode, allowance.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<OverbookingAllowanceResponse>.Success(Map(allowance));
    }

    /// <summary>
    /// Deux autorisations actives qui se chevauchent sur le meme type rendraient le solde ambigu :
    /// le calcul retient la plus large, mais un parametrage ambigu se lit mal et se corrige mal.
    /// </summary>
    private async Task<ApplicationResult<OverbookingAllowanceResponse>?> RequireNoOverlappingAllowanceAsync(
        OverbookingAllowance allowance,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<OverbookingAllowance>()
            .AsNoTracking()
            .Where(current => current.Id != allowance.Id
                && current.HotelUnitCode == allowance.HotelUnitCode
                && current.RoomTypeCode == allowance.RoomTypeCode
                && current.IsActive
                && current.FromDate <= allowance.ToDate
                && allowance.FromDate <= current.ToDate)
            .ToArrayAsync(cancellationToken);

        if (candidates.Length == 0)
        {
            return null;
        }

        var conflict = candidates[0];

        return ApplicationResult<OverbookingAllowanceResponse>.Conflict(
            $"Une autorisation active couvre deja le type {allowance.RoomTypeCode} du "
            + $"{conflict.FromDate:dd/MM/yyyy} au {conflict.ToDate:dd/MM/yyyy}. Modifiez-la plutot que "
            + "d'en ajouter une seconde.");
    }

    private static OverbookingAllowanceResponse Map(OverbookingAllowance allowance)
    {
        return new OverbookingAllowanceResponse(
            allowance.Id,
            allowance.HotelUnitCode,
            allowance.RoomTypeCode,
            allowance.FromDate,
            allowance.ToDate,
            allowance.ExtraRooms,
            allowance.IsActive,
            allowance.Notes,
            allowance.CreatedAt,
            allowance.CreatedBy,
            allowance.UpdatedAt,
            allowance.UpdatedBy);
    }
}
