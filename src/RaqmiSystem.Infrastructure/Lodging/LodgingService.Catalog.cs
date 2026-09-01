using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Les referentiels commerciaux : extras, forfaits, politiques d'annulation, regles de yield.
/// </summary>
public sealed partial class LodgingService
{
    // ==================================== Referentiel extras ====================================

    public async Task<ApplicationResult<IReadOnlyCollection<ExtraItemResponse>>> ListExtrasAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<ExtraItemResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<ExtraItem>()
            .AsNoTracking()
            .Where(item => item.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var items = await query
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Code)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<ExtraItemResponse>>.Success(items.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<ExtraItemResponse>> CreateExtraAsync(
        SaveExtraItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<ExtraItemResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        ExtraItem item;

        try
        {
            item = new ExtraItem(
                unitFailure.UnitCode,
                request.Code,
                request.Label,
                request.PricingBasis,
                request.UnitPrice,
                request.VatRate,
                request.ChargeKind,
                request.Description);

            item.SetPostedByNightAudit(request.IsPostedByNightAudit);
            item.UpdateDetails(
                request.Label,
                request.PricingBasis,
                request.UnitPrice,
                request.VatRate,
                request.ChargeKind,
                request.Description,
                request.DisplayOrder);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ExtraItemResponse>.Validation(ex.Message);
        }

        item.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<ExtraItem>().Add(item);

        await WriteAuditAsync(
            "lodging.extra.created",
            ExtrasEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.Label, item.UnitPrice, Basis = item.PricingBasis.ToString() },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<ExtraItemResponse>.Conflict(
                "Un extra portant ce code existe deja dans cette unite.");
        }

        return ApplicationResult<ExtraItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<ExtraItemResponse>> UpdateExtraAsync(
        Guid id,
        SaveExtraItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<ExtraItem>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<ExtraItemResponse>.NotFound("L'extra est introuvable.");
        }

        try
        {
            item.UpdateDetails(
                request.Label,
                request.PricingBasis,
                request.UnitPrice,
                request.VatRate,
                request.ChargeKind,
                request.Description,
                request.DisplayOrder);

            item.SetPostedByNightAudit(request.IsPostedByNightAudit);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ExtraItemResponse>.Validation(ex.Message);
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.extra.updated",
            ExtrasEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.Label, item.UnitPrice },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ExtraItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<ExtraItemResponse>> SetExtraActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<ExtraItem>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<ExtraItemResponse>.NotFound("L'extra est introuvable.");
        }

        if (isActive)
        {
            item.Activate();
        }
        else
        {
            item.Deactivate();
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.extra.activated" : "lodging.extra.deactivated",
            ExtrasEntity,
            item.Id,
            context,
            new { item.HotelUnitCode, item.Code, item.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ExtraItemResponse>.Success(Map(item));
    }

    private static ExtraItemResponse Map(ExtraItem item)
    {
        return new ExtraItemResponse(
            item.Id,
            item.HotelUnitCode,
            item.Code,
            item.Label,
            item.Description,
            item.PricingBasis,
            item.UnitPrice,
            item.VatRate,
            item.ChargeKind,
            item.IsPostedByNightAudit,
            item.IsActive,
            item.DisplayOrder,
            item.CreatedAt,
            item.CreatedBy,
            item.UpdatedAt,
            item.UpdatedBy);
    }

    // ======================================== Forfaits ========================================

    public async Task<ApplicationResult<IReadOnlyCollection<PackageResponse>>> ListPackagesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<PackageResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<Package>()
            .AsNoTracking()
            .Include(package => package.Components)
            .Where(package => package.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(package => package.IsActive);
        }

        var packages = await query
            .OrderBy(package => package.Code)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<PackageResponse>>.Success(packages.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<PackageResponse>> CreatePackageAsync(
        SavePackageRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<PackageResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        Package package;

        try
        {
            package = new Package(
                unitFailure.UnitCode,
                request.Code,
                request.Label,
                request.TotalPrice,
                request.Description);

            package.UpdateDetails(request.Label, request.TotalPrice, request.Description, request.Nights);
            package.SetScope(request.RatePlanCode, request.RoomTypeCode, request.ValidFrom, request.ValidTo);
            package.ReplaceComponents(request.Components.Select(ToComponent));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PackageResponse>.Validation(ex.Message);
        }

        package.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Package>().Add(package);

        await WriteAuditAsync(
            "lodging.package.created",
            PackagesEntity,
            package.Id,
            context,
            new { package.HotelUnitCode, package.Code, package.Label, package.TotalPrice, package.IsBalanced },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<PackageResponse>.Conflict(
                "Un forfait portant ce code existe deja dans cette unite.");
        }

        return ApplicationResult<PackageResponse>.Success(Map(package));
    }

    public async Task<ApplicationResult<PackageResponse>> UpdatePackageAsync(
        Guid id,
        SavePackageRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.Set<Package>()
            .Include(current => current.Components)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (package is null)
        {
            return ApplicationResult<PackageResponse>.NotFound("Le forfait est introuvable.");
        }

        try
        {
            package.UpdateDetails(request.Label, request.TotalPrice, request.Description, request.Nights);
            package.SetScope(request.RatePlanCode, request.RoomTypeCode, request.ValidFrom, request.ValidTo);
            package.ReplaceComponents(request.Components.Select(ToComponent));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PackageResponse>.Validation(ex.Message);
        }

        package.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.package.updated",
            PackagesEntity,
            package.Id,
            context,
            new { package.HotelUnitCode, package.Code, package.TotalPrice, package.IsBalanced },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<PackageResponse>.Success(Map(package));
    }

    public async Task<ApplicationResult<PackageResponse>> SetPackageActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.Set<Package>()
            .Include(current => current.Components)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (package is null)
        {
            return ApplicationResult<PackageResponse>.NotFound("Le forfait est introuvable.");
        }

        if (isActive)
        {
            // ACTIVER UN FORFAIT NON EQUILIBRE EST REFUSE. Le vendre repartirait son chiffre au
            // hasard entre les services, et personne ne s'en apercevrait avant la cloture.
            if (!package.IsBalanced)
            {
                return ApplicationResult<PackageResponse>.Validation(
                    $"La ventilation du forfait totalise {package.ComponentsTotal:0.00} alors que son prix "
                    + $"global est {package.TotalPrice:0.00}. Equilibrez-la avant de l'activer.");
            }

            package.Activate();
        }
        else
        {
            package.Deactivate();
        }

        package.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.package.activated" : "lodging.package.deactivated",
            PackagesEntity,
            package.Id,
            context,
            new { package.HotelUnitCode, package.Code, package.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<PackageResponse>.Success(Map(package));
    }

    private static PackageComponent ToComponent(PackageComponentResponse line)
    {
        return new PackageComponent(
            line.Label,
            line.Amount,
            line.ChargeKind,
            line.ExtraCode,
            line.PricingBasis);
    }

    private static PackageResponse Map(Package package)
    {
        return new PackageResponse(
            package.Id,
            package.HotelUnitCode,
            package.Code,
            package.Label,
            package.Description,
            package.TotalPrice,
            package.ComponentsTotal,
            package.IsBalanced,
            package.RatePlanCode,
            package.RoomTypeCode,
            package.ValidFrom,
            package.ValidTo,
            package.Nights,
            package.IsActive,
            package.Components
                .Select(component => new PackageComponentResponse(
                    component.Label,
                    component.Amount,
                    component.ChargeKind,
                    component.ExtraCode,
                    component.PricingBasis))
                .ToArray(),
            package.CreatedAt,
            package.CreatedBy,
            package.UpdatedAt,
            package.UpdatedBy);
    }

    // =============================== Politiques d'annulation ===============================

    public async Task<ApplicationResult<IReadOnlyCollection<CancellationPolicyResponse>>> ListCancellationPoliciesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<CancellationPolicyResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<CancellationPolicy>()
            .AsNoTracking()
            .Include(policy => policy.Rules)
            .Where(policy => policy.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(policy => policy.IsActive);
        }

        var policies = await query.OrderBy(policy => policy.Code).ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<CancellationPolicyResponse>>.Success(
            policies.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<CancellationPolicyResponse>> CreateCancellationPolicyAsync(
        SaveCancellationPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<CancellationPolicyResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        CancellationPolicy policy;

        try
        {
            policy = new CancellationPolicy(
                unitFailure.UnitCode,
                request.Code,
                request.Label,
                request.Description);

            policy.SetNoShowTerms(request.NoShowBasis, request.NoShowValue);
            policy.ReplaceRules(request.Rules.Select(ToRule));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CancellationPolicyResponse>.Validation(ex.Message);
        }

        policy.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<CancellationPolicy>().Add(policy);

        await WriteAuditAsync(
            "lodging.cancellation_policy.created",
            CancellationPoliciesEntity,
            policy.Id,
            context,
            new { policy.HotelUnitCode, policy.Code, policy.Label, RuleCount = policy.Rules.Count },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<CancellationPolicyResponse>.Conflict(
                "Une politique portant ce code existe deja dans cette unite.");
        }

        return ApplicationResult<CancellationPolicyResponse>.Success(Map(policy));
    }

    public async Task<ApplicationResult<CancellationPolicyResponse>> UpdateCancellationPolicyAsync(
        Guid id,
        SaveCancellationPolicyRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.Set<CancellationPolicy>()
            .Include(current => current.Rules)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (policy is null)
        {
            return ApplicationResult<CancellationPolicyResponse>.NotFound("La politique est introuvable.");
        }

        try
        {
            policy.UpdateDetails(request.Label, request.Description);
            policy.SetNoShowTerms(request.NoShowBasis, request.NoShowValue);
            policy.ReplaceRules(request.Rules.Select(ToRule));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CancellationPolicyResponse>.Validation(ex.Message);
        }

        policy.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        // Aucune reservation deja prise n'est touchee : chacune porte sa propre copie figee de la
        // politique. C'est tout l'objet du figement, et c'est ce qui rend cette modification sure.
        await WriteAuditAsync(
            "lodging.cancellation_policy.updated",
            CancellationPoliciesEntity,
            policy.Id,
            context,
            new { policy.HotelUnitCode, policy.Code, RuleCount = policy.Rules.Count },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CancellationPolicyResponse>.Success(Map(policy));
    }

    public async Task<ApplicationResult<CancellationPolicyResponse>> SetCancellationPolicyActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.Set<CancellationPolicy>()
            .Include(current => current.Rules)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (policy is null)
        {
            return ApplicationResult<CancellationPolicyResponse>.NotFound("La politique est introuvable.");
        }

        if (isActive)
        {
            policy.Activate();
        }
        else
        {
            policy.Deactivate();
        }

        policy.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.cancellation_policy.activated" : "lodging.cancellation_policy.deactivated",
            CancellationPoliciesEntity,
            policy.Id,
            context,
            new { policy.HotelUnitCode, policy.Code, policy.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CancellationPolicyResponse>.Success(Map(policy));
    }

    private static CancellationPolicyRule ToRule(CancellationPolicyRuleResponse line)
    {
        return new CancellationPolicyRule(line.MinDaysBeforeArrival, line.Basis, line.Value);
    }

    private static CancellationPolicyResponse Map(CancellationPolicy policy)
    {
        return new CancellationPolicyResponse(
            policy.Id,
            policy.HotelUnitCode,
            policy.Code,
            policy.Label,
            policy.Description,
            policy.IsActive,
            policy.NoShowBasis,
            policy.NoShowValue,
            policy.Rules
                .OrderByDescending(rule => rule.MinDaysBeforeArrival)
                .Select(rule => new CancellationPolicyRuleResponse(
                    rule.MinDaysBeforeArrival,
                    rule.Basis,
                    rule.Value))
                .ToArray(),
            CancellationPolicy.DescribeSnapshot(policy.ToSnapshotJson()),
            policy.CreatedAt,
            policy.CreatedBy,
            policy.UpdatedAt,
            policy.UpdatedBy);
    }

    // ================================== Revenue management ==================================

    public async Task<ApplicationResult<IReadOnlyCollection<YieldRuleResponse>>> ListYieldRulesAsync(
        string hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<YieldRuleResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<YieldRule>()
            .AsNoTracking()
            .Where(rule => rule.HotelUnitCode == normalizedUnitCode);

        if (!includeInactive)
        {
            query = query.Where(rule => rule.IsActive);
        }

        var rules = await query
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Code)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<YieldRuleResponse>>.Success(rules.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<YieldRuleResponse>> CreateYieldRuleAsync(
        SaveYieldRuleRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<YieldRuleResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        YieldRule rule;

        try
        {
            rule = new YieldRule(
                unitFailure.UnitCode,
                request.Code,
                request.Label,
                request.FromDate,
                request.ToDate,
                request.Trigger,
                request.ThresholdValue,
                request.AdjustmentPercent,
                request.Priority,
                request.RoomTypeCode,
                request.RatePlanCode);

            rule.SetDaysOfWeek(ParseDays(request.DaysOfWeek));
            rule.SetNotes(request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<YieldRuleResponse>.Validation(ex.Message);
        }

        if (rule.Trigger == YieldTrigger.DayOfWeek && rule.DaysOfWeek is null)
        {
            return ApplicationResult<YieldRuleResponse>.Validation(
                "Une regle declenchee par jour de semaine doit declarer au moins un jour.");
        }

        rule.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<YieldRule>().Add(rule);

        await WriteAuditAsync(
            "lodging.yield_rule.created",
            YieldRulesEntity,
            rule.Id,
            context,
            new
            {
                rule.HotelUnitCode,
                rule.Code,
                Trigger = rule.Trigger.ToString(),
                rule.ThresholdValue,
                rule.AdjustmentPercent,
                rule.Priority
            },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<YieldRuleResponse>.Conflict(
                "Une regle portant ce code existe deja dans cette unite.");
        }

        return ApplicationResult<YieldRuleResponse>.Success(Map(rule));
    }

    public async Task<ApplicationResult<YieldRuleResponse>> UpdateYieldRuleAsync(
        Guid id,
        SaveYieldRuleRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.Set<YieldRule>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (rule is null)
        {
            return ApplicationResult<YieldRuleResponse>.NotFound("La regle de yield est introuvable.");
        }

        try
        {
            rule.UpdateTerms(
                request.FromDate,
                request.ToDate,
                request.ThresholdValue,
                request.AdjustmentPercent,
                request.Priority);

            rule.SetScope(request.RoomTypeCode, request.RatePlanCode);
            rule.SetDaysOfWeek(ParseDays(request.DaysOfWeek));
            rule.SetNotes(request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<YieldRuleResponse>.Validation(ex.Message);
        }

        rule.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.yield_rule.updated",
            YieldRulesEntity,
            rule.Id,
            context,
            new { rule.HotelUnitCode, rule.Code, rule.AdjustmentPercent, rule.Priority },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<YieldRuleResponse>.Success(Map(rule));
    }

    public async Task<ApplicationResult<YieldRuleResponse>> SetYieldRuleActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.Set<YieldRule>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (rule is null)
        {
            return ApplicationResult<YieldRuleResponse>.NotFound("La regle de yield est introuvable.");
        }

        if (isActive)
        {
            rule.Activate();
        }
        else
        {
            rule.Deactivate();
        }

        rule.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.yield_rule.activated" : "lodging.yield_rule.deactivated",
            YieldRulesEntity,
            rule.Id,
            context,
            new { rule.HotelUnitCode, rule.Code, rule.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<YieldRuleResponse>.Success(Map(rule));
    }

    private static IEnumerable<DayOfWeek>? ParseDays(IReadOnlyCollection<string>? days)
    {
        if (days is null)
        {
            return null;
        }

        var parsed = new List<DayOfWeek>();

        foreach (var day in days)
        {
            var code = day.Trim().ToUpperInvariant();

            var value = code switch
            {
                "MON" or "MONDAY" or "LUN" => DayOfWeek.Monday,
                "TUE" or "TUESDAY" or "MAR" => DayOfWeek.Tuesday,
                "WED" or "WEDNESDAY" or "MER" => DayOfWeek.Wednesday,
                "THU" or "THURSDAY" or "JEU" => DayOfWeek.Thursday,
                "FRI" or "FRIDAY" or "VEN" => DayOfWeek.Friday,
                "SAT" or "SATURDAY" or "SAM" => DayOfWeek.Saturday,
                "SUN" or "SUNDAY" or "DIM" => DayOfWeek.Sunday,
                _ => throw new ArgumentException($"Jour de semaine inconnu : {day}.", nameof(days))
            };

            parsed.Add(value);
        }

        return parsed;
    }

    private static YieldRuleResponse Map(YieldRule rule)
    {
        return new YieldRuleResponse(
            rule.Id,
            rule.HotelUnitCode,
            rule.Code,
            rule.Label,
            rule.RoomTypeCode,
            rule.RatePlanCode,
            rule.FromDate,
            rule.ToDate,
            rule.Trigger,
            rule.ThresholdValue,
            rule.DaysOfWeek,
            rule.AdjustmentPercent,
            rule.Priority,
            rule.IsActive,
            rule.Notes,
            rule.CreatedAt,
            rule.CreatedBy,
            rule.UpdatedAt,
            rule.UpdatedBy);
    }
}
