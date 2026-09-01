using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// Le parametrage de la bibliotheque KPI : bornes d'alerte, rattachement des comptes aux
/// groupes de gestion, pose et cloture des instantanes. Les trois seuls actes d'ECRITURE du
/// module, tous derriere la cle <c>kpi.admin</c>, tous audites - fixer l'aune a laquelle les
/// unites sont jugees et figer un chiffre communique sont des actes de gouvernance.
///
/// LA CLOTURE NE CORRIGE JAMAIS. Un instantane cloture qui diverge du recalcul est signale dans
/// la reponse et laisse tel quel : la meme discipline que la comptabilite, ou une ecriture
/// comptabilisee se corrige par une extourne et jamais par une modification.
/// </summary>
public sealed class KpiAdministrationService(
    RaqmiDbContext dbContext,
    KpiFactLoader factLoader,
    IAuditLogWriter auditLogWriter) : IKpiAdministrationService
{
    private readonly KpiEngine engine = new();

    public async Task<ApplicationResult<IReadOnlyCollection<KpiThresholdResponse>>> GetThresholdsAsync(
        string? kpiCode,
        string? hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<KpiThreshold>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(kpiCode))
        {
            var definition = KpiCatalog.Find(kpiCode);

            if (definition is null)
            {
                return ApplicationResult<IReadOnlyCollection<KpiThresholdResponse>>.NotFound(
                    $"L'indicateur {kpiCode} n'existe pas dans le catalogue.");
            }

            query = query.Where(threshold => threshold.KpiCode == definition.Code);
        }

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            var scopeKey = KpiScopeKey.For(hotelUnitCode);
            query = query.Where(threshold => threshold.ScopeKey == scopeKey);
        }

        var thresholds = await query
            .OrderBy(threshold => threshold.KpiCode)
            .ThenBy(threshold => threshold.ScopeKey)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<KpiThresholdResponse>>.Success(
            thresholds.Select(ToResponse).ToArray());
    }

    /// <summary>
    /// Cree ou REMPLACE la regle du couple (indicateur, perimetre) : reparametrer n'empile
    /// jamais deux regles concurrentes dont personne ne saurait laquelle s'applique. L'index
    /// unique sur (kpi_code, scope_key) arbitre les creations concurrentes - le perdant recoit
    /// un conflit, jamais un doublon.
    /// </summary>
    public async Task<ApplicationResult<KpiThresholdResponse>> SaveThresholdAsync(
        SaveKpiThresholdRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var definition = KpiCatalog.Find(request.KpiCode);

        if (definition is null)
        {
            return ApplicationResult<KpiThresholdResponse>.NotFound(
                $"L'indicateur {request.KpiCode} n'existe pas dans le catalogue.");
        }

        if (!string.IsNullOrWhiteSpace(request.HotelUnitCode))
        {
            var normalized = HotelUnit.NormalizeCode(request.HotelUnitCode);
            var unitExists = await dbContext.Set<HotelUnit>()
                .AsNoTracking()
                .AnyAsync(unit => unit.Code == normalized, cancellationToken);

            if (!unitExists)
            {
                return ApplicationResult<KpiThresholdResponse>.NotFound(
                    $"L'unite {request.HotelUnitCode} n'existe pas.");
            }
        }

        var scopeKey = KpiScopeKey.For(request.HotelUnitCode);

        var existing = await dbContext.Set<KpiThreshold>()
            .SingleOrDefaultAsync(
                threshold => threshold.KpiCode == definition.Code && threshold.ScopeKey == scopeKey,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        KpiThreshold threshold;

        try
        {
            if (existing is null)
            {
                threshold = new KpiThreshold(
                    definition.Code,
                    request.HotelUnitCode,
                    request.FavorableThreshold,
                    request.CriticalThreshold,
                    request.TargetValue,
                    request.OwnerRole,
                    request.Notes);

                threshold.MarkCreated(context.UserName, now);
                dbContext.Set<KpiThreshold>().Add(threshold);
            }
            else
            {
                existing.Apply(
                    request.FavorableThreshold,
                    request.CriticalThreshold,
                    request.TargetValue,
                    request.OwnerRole,
                    request.Notes);

                existing.Activate();
                existing.MarkUpdated(context.UserName, now);
                threshold = existing;
            }
        }
        catch (ArgumentException exception)
        {
            // La coherence des bornes avec le sens de lecture est une regle du domaine ; son
            // refus est une erreur de saisie, pas une erreur serveur.
            return ApplicationResult<KpiThresholdResponse>.Validation(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            "kpi.threshold.saved",
            nameof(KpiThreshold),
            threshold.Id,
            new
            {
                threshold.KpiCode,
                threshold.HotelUnitCode,
                threshold.FavorableThreshold,
                threshold.CriticalThreshold,
                threshold.TargetValue,
                threshold.OwnerRole
            },
            cancellationToken);

        return ApplicationResult<KpiThresholdResponse>.Success(ToResponse(threshold));
    }

    public async Task<ApplicationResult<KpiThresholdResponse>> SetThresholdActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var threshold = await dbContext.Set<KpiThreshold>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (threshold is null)
        {
            return ApplicationResult<KpiThresholdResponse>.NotFound("Cette regle de seuils n'existe pas.");
        }

        if (isActive)
        {
            threshold.Activate();
        }
        else
        {
            threshold.Deactivate();
        }

        threshold.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            isActive ? "kpi.threshold.activated" : "kpi.threshold.deactivated",
            nameof(KpiThreshold),
            threshold.Id,
            new { threshold.KpiCode, threshold.HotelUnitCode },
            cancellationToken);

        return ApplicationResult<KpiThresholdResponse>.Success(ToResponse(threshold));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<KpiAccountMappingResponse>>> GetAccountMappingsAsync(
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.Set<KpiAccountMapping>()
            .AsNoTracking()
            .OrderBy(mapping => mapping.AccountPrefix)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<KpiAccountMappingResponse>>.Success(
            mappings.Select(ToResponse).ToArray());
    }

    public async Task<ApplicationResult<KpiAccountMappingResponse>> SaveAccountMappingAsync(
        SaveKpiAccountMappingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        string prefix;

        try
        {
            prefix = KpiAccountMapping.NormalizePrefix(request.AccountPrefix);
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<KpiAccountMappingResponse>.Validation(exception.Message);
        }

        var existing = await dbContext.Set<KpiAccountMapping>()
            .SingleOrDefaultAsync(mapping => mapping.AccountPrefix == prefix, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        KpiAccountMapping mapping;

        try
        {
            if (existing is null)
            {
                mapping = new KpiAccountMapping(prefix, request.Group, request.Label);
                mapping.MarkCreated(context.UserName, now);
                dbContext.Set<KpiAccountMapping>().Add(mapping);
            }
            else
            {
                existing.UpdateDetails(request.Group, request.Label);
                existing.Activate();
                existing.MarkUpdated(context.UserName, now);
                mapping = existing;
            }
        }
        catch (ArgumentException exception)
        {
            return ApplicationResult<KpiAccountMappingResponse>.Validation(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            "kpi.account_mapping.saved",
            nameof(KpiAccountMapping),
            mapping.Id,
            new { mapping.AccountPrefix, Group = mapping.Group.ToString(), mapping.Label },
            cancellationToken);

        return ApplicationResult<KpiAccountMappingResponse>.Success(ToResponse(mapping));
    }

    public async Task<ApplicationResult<KpiAccountMappingResponse>> SetAccountMappingActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mapping = await dbContext.Set<KpiAccountMapping>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (mapping is null)
        {
            return ApplicationResult<KpiAccountMappingResponse>.NotFound(
                "Ce rattachement de comptes n'existe pas.");
        }

        if (isActive)
        {
            mapping.Activate();
        }
        else
        {
            mapping.Deactivate();
        }

        mapping.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            isActive ? "kpi.account_mapping.activated" : "kpi.account_mapping.deactivated",
            nameof(KpiAccountMapping),
            mapping.Id,
            new { mapping.AccountPrefix },
            cancellationToken);

        return ApplicationResult<KpiAccountMappingResponse>.Success(ToResponse(mapping));
    }

    /// <summary>
    /// Calcule la periode et pose un instantane par indicateur retenu et par perimetre (groupe
    /// et chaque unite). Idempotent par construction : un instantane provisoire deja pose est
    /// rafraichi, un instantane CLOTURE n'est jamais touche - s'il diverge du recalcul, la
    /// divergence est comptee et nommee dans la reponse, a charge d'un humain de decider.
    ///
    /// Le calcul tourne sous un contexte sans restriction : la pose d'instantanes historise la
    /// bibliotheque pour tout le monde et ne doit pas dependre du profil de qui la declenche -
    /// la LECTURE de ces instantanes, elle, repasse par le filtre de permissions.
    /// </summary>
    public async Task<ApplicationResult<KpiSnapshotBatchResponse>> CaptureSnapshotsAsync(
        CaptureKpiSnapshotsRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var (codes, error) = ResolveCodes(request.Codes);

        if (error is not null)
        {
            return ApplicationResult<KpiSnapshotBatchResponse>.Validation(error);
        }

        if (request.To < request.From)
        {
            return ApplicationResult<KpiSnapshotBatchResponse>.Validation(
                "La date de debut ne peut pas depasser la date de fin.");
        }

        if (request.To.DayNumber - request.From.DayNumber + 1 > KpiQuery.MaxWindowDays)
        {
            return ApplicationResult<KpiSnapshotBatchResponse>.Validation(
                $"La fenetre d'analyse ne peut pas depasser {KpiQuery.MaxWindowDays} jours.");
        }

        var period = KpiPeriod.Create(request.From, request.To);
        var facts = await factLoader.LoadAsync(period.From, period.To, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var computation = engine.Compute(period, facts, today, request.DsoMethod);

        var existing = await dbContext.Set<KpiSnapshot>()
            .Where(snapshot => snapshot.PeriodStart == period.From && snapshot.PeriodEnd == period.To)
            .ToDictionaryAsync(
                snapshot => (snapshot.KpiCode, snapshot.ScopeKey),
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var created = 0;
        var refreshed = 0;
        var skipped = 0;
        var divergences = new List<string>();

        var scopes = new List<string?> { null };
        scopes.AddRange(facts.Units.Select(unit => (string?)unit.Code));

        foreach (var code in codes)
        {
            foreach (var scope in scopes)
            {
                var measure = computation.Require(code, scope);

                // Un indicateur "sans objet" sur ce perimetre (en attente de source, ou groupe
                // seulement) n'est pas historise : conserver des lignes vides par construction
                // remplirait la table sans que personne ne les relise jamais.
                if (measure.Quality == KpiQuality.NotApplicable)
                {
                    continue;
                }

                var scopeKey = KpiScopeKey.For(scope);

                if (existing.TryGetValue((code, scopeKey), out var snapshot))
                {
                    if (snapshot.IsClosed)
                    {
                        skipped++;

                        if (snapshot.DivergesFrom(measure.Value))
                        {
                            divergences.Add(
                                $"{code} / {scope ?? "groupe"} : valeur figee {Format(snapshot.Value)}, "
                                + $"recalcul {Format(measure.Value)}.");
                        }

                        continue;
                    }

                    snapshot.Refresh(
                        measure.Value,
                        measure.Numerator,
                        measure.Denominator,
                        measure.Quality,
                        KpiCatalog.Require(code).FormulaVersion,
                        now);

                    snapshot.MarkUpdated(context.UserName, now);
                    refreshed++;
                    continue;
                }

                var fresh = new KpiSnapshot(
                    code,
                    scope,
                    period.From,
                    period.To,
                    period.Granularity,
                    measure.Value,
                    measure.Numerator,
                    measure.Denominator,
                    measure.Quality,
                    KpiCatalog.Require(code).FormulaVersion,
                    now);

                fresh.MarkCreated(context.UserName, now);
                dbContext.Set<KpiSnapshot>().Add(fresh);
                created++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            "kpi.snapshots.captured",
            nameof(KpiSnapshot),
            null,
            new { period.From, period.To, Created = created, Refreshed = refreshed, Skipped = skipped },
            cancellationToken);

        return ApplicationResult<KpiSnapshotBatchResponse>.Success(new KpiSnapshotBatchResponse(
            period.From,
            period.To,
            created,
            refreshed,
            Closed: 0,
            skipped,
            divergences));
    }

    /// <summary>
    /// Fige les instantanes PROVISOIRES de la periode. Irreversible par construction - l'entite
    /// elle-meme refuse tout recalcul ensuite - et c'est ce qui rend un chiffre communique
    /// retrouvable a l'identique des mois plus tard. Un instantane deja cloture est laisse tel
    /// quel et compte a part : cloturer deux fois ecraserait la trace de qui a fige le chiffre.
    /// </summary>
    public async Task<ApplicationResult<KpiSnapshotBatchResponse>> CloseSnapshotsAsync(
        CloseKpiSnapshotsRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var (codes, error) = ResolveCodes(request.Codes);

        if (error is not null)
        {
            return ApplicationResult<KpiSnapshotBatchResponse>.Validation(error);
        }

        var snapshots = await dbContext.Set<KpiSnapshot>()
            .Where(snapshot => snapshot.PeriodStart == request.From && snapshot.PeriodEnd == request.To)
            .ToArrayAsync(cancellationToken);

        var inScope = snapshots
            .Where(snapshot => codes.Contains(snapshot.KpiCode, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (inScope.Length == 0)
        {
            return ApplicationResult<KpiSnapshotBatchResponse>.NotFound(
                "Aucun instantane n'existe sur cette periode : posez d'abord les instantanes, puis cloturez-les.");
        }

        var now = DateTimeOffset.UtcNow;
        var closed = 0;
        var alreadyClosed = 0;

        foreach (var snapshot in inScope)
        {
            if (snapshot.IsClosed)
            {
                alreadyClosed++;
                continue;
            }

            snapshot.Close(context.UserName, now);
            snapshot.MarkUpdated(context.UserName, now);
            closed++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            context,
            "kpi.snapshots.closed",
            nameof(KpiSnapshot),
            null,
            new { request.From, request.To, Closed = closed, AlreadyClosed = alreadyClosed },
            cancellationToken);

        return ApplicationResult<KpiSnapshotBatchResponse>.Success(new KpiSnapshotBatchResponse(
            request.From,
            request.To,
            Created: 0,
            Refreshed: 0,
            closed,
            alreadyClosed,
            []));
    }

    /// <summary>
    /// Les codes demandes, ou par defaut tous les indicateurs CALCULABLES du catalogue. Un code
    /// inconnu est une erreur de saisie nommee, jamais ignoree en silence - une cloture qui
    /// n'aurait pas fige l'indicateur qu'on croyait serait pire qu'un refus.
    /// </summary>
    private static (string[] Codes, string? Error) ResolveCodes(IReadOnlyCollection<string>? requested)
    {
        if (requested is null || requested.Count == 0)
        {
            return (KpiCatalog.All
                .Where(definition => definition.IsComputable)
                .Select(definition => definition.Code)
                .ToArray(), null);
        }

        var resolved = new List<string>();

        foreach (var code in requested)
        {
            var definition = KpiCatalog.Find(code);

            if (definition is null)
            {
                return ([], $"L'indicateur {code} n'existe pas dans le catalogue.");
            }

            resolved.Add(definition.Code);
        }

        return ([.. resolved.Distinct(StringComparer.OrdinalIgnoreCase)], null);
    }

    private async Task WriteAuditAsync(
        OperationContext context,
        string action,
        string entityName,
        Guid? entityId,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId?.ToString() ?? "batch",
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }

    private static KpiThresholdResponse ToResponse(KpiThreshold threshold)
    {
        var definition = KpiCatalog.Require(threshold.KpiCode);

        return new KpiThresholdResponse(
            threshold.Id,
            threshold.KpiCode,
            definition.Name,
            definition.Unit,
            definition.Polarity,
            threshold.HotelUnitCode,
            threshold.FavorableThreshold,
            threshold.CriticalThreshold,
            threshold.TargetValue,
            threshold.OwnerRole,
            threshold.Notes,
            threshold.IsActive);
    }

    private static KpiAccountMappingResponse ToResponse(KpiAccountMapping mapping)
    {
        return new KpiAccountMappingResponse(
            mapping.Id,
            mapping.AccountPrefix,
            mapping.Group,
            mapping.Label,
            mapping.IsActive);
    }

    private static string Format(decimal? value)
    {
        return value is null
            ? "-"
            : value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
