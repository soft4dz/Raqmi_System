using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// La lecture de la bibliotheque KPI : catalogue, tableau de bord, indicateur unitaire,
/// historique, comparatif inter-unites et alertes.
///
/// Ce service ne calcule RIEN lui-meme : il charge les faits (<see cref="KpiFactLoader"/>),
/// les seuils et les instantanes, puis delegue au moteur pur (<see cref="KpiEngine"/>) et au
/// constructeur de reponses (<see cref="KpiDashboardBuilder"/>), qui possedent toutes les
/// regles et sont testes sans base. Il ne possede aucune table de donnees metier et n'ecrit
/// jamais rien - une lecture pure n'a pas de trace d'audit a laisser.
///
/// LE FILTRE DE PERMISSIONS est applique ici, cote serveur, via le KpiAccessContext construit
/// par l'endpoint a partir des revendications reelles du jeton : un indicateur que le profil
/// n'a pas le droit de lire ne quitte jamais le serveur, et la reponse dit combien de lignes
/// elle ne montre pas.
/// </summary>
public sealed class KpiService(
    RaqmiDbContext dbContext,
    KpiFactLoader factLoader) : IKpiService
{
    private readonly KpiEngine engine = new();
    private readonly KpiDashboardBuilder builder = new();

    public Task<ApplicationResult<KpiCatalogResponse>> GetCatalogAsync(
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);

        var definitions = KpiCatalog.All
            .Select(definition => KpiDefinitionResponse.From(definition, access.CanRead(definition)))
            .ToArray();

        var response = new KpiCatalogResponse(
            definitions.Length,
            definitions.Count(definition => definition.Availability == KpiAvailability.Implemented),
            definitions.Count(definition => definition.Availability == KpiAvailability.AwaitingSource),
            definitions.Count(definition => definition.Readable),
            definitions);

        return Task.FromResult(ApplicationResult<KpiCatalogResponse>.Success(response));
    }

    public async Task<ApplicationResult<KpiDashboardResponse>> GetDashboardAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(query, cancellationToken);

        if (validation is not null)
        {
            return ApplicationResult<KpiDashboardResponse>.Validation(validation);
        }

        var context = await ComputeAsync(query, cancellationToken);

        var response = builder.BuildDashboard(
            query,
            context.Current,
            context.Previous,
            context.Units,
            context.Thresholds,
            context.Snapshots,
            access,
            DateTimeOffset.UtcNow);

        return ApplicationResult<KpiDashboardResponse>.Success(response);
    }

    public async Task<ApplicationResult<KpiMeasureResponse>> GetMeasureAsync(
        string code,
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);

        var definition = KpiCatalog.Find(code);

        if (definition is null)
        {
            return ApplicationResult<KpiMeasureResponse>.NotFound(
                $"L'indicateur {code} n'existe pas dans le catalogue.");
        }

        // Le refus de permission est un refus EXPLICITE, jamais un NotFound : cacher l'existence
        // d'un indicateur du catalogue public n'apporte rien, et dire quelle cle manque permet a
        // l'administrateur d'ajuster le profil.
        if (!access.CanRead(definition))
        {
            return ApplicationResult<KpiMeasureResponse>.Validation(
                $"La lecture de {definition.Code} exige les permissions suivantes : "
                + string.Join(", ", definition.RequiredPermissions) + ".");
        }

        var validation = await ValidateAsync(query, cancellationToken);

        if (validation is not null)
        {
            return ApplicationResult<KpiMeasureResponse>.Validation(validation);
        }

        var context = await ComputeAsync(query, cancellationToken);

        var dashboard = builder.BuildDashboard(
            query,
            context.Current,
            context.Previous,
            context.Units,
            context.Thresholds,
            context.Snapshots,
            access,
            DateTimeOffset.UtcNow);

        var measure = dashboard.Sections
            .SelectMany(section => section.Measures)
            .FirstOrDefault(candidate => candidate.Code == definition.Code);

        return measure is null
            ? ApplicationResult<KpiMeasureResponse>.NotFound(
                $"L'indicateur {definition.Code} n'a pas ete calcule sur ce perimetre.")
            : ApplicationResult<KpiMeasureResponse>.Success(measure);
    }

    public async Task<ApplicationResult<KpiHistoryResponse>> GetHistoryAsync(
        string code,
        string? hotelUnitCode,
        DateOnly from,
        DateOnly to,
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);

        var definition = KpiCatalog.Find(code);

        if (definition is null)
        {
            return ApplicationResult<KpiHistoryResponse>.NotFound(
                $"L'indicateur {code} n'existe pas dans le catalogue.");
        }

        if (!access.CanRead(definition))
        {
            return ApplicationResult<KpiHistoryResponse>.Validation(
                $"La lecture de {definition.Code} exige les permissions suivantes : "
                + string.Join(", ", definition.RequiredPermissions) + ".");
        }

        if (to < from)
        {
            return ApplicationResult<KpiHistoryResponse>.Validation(
                "La date de debut ne peut pas depasser la date de fin.");
        }

        var scopeKey = KpiScopeKey.For(hotelUnitCode);

        // L'historique REND ce qui a ete conserve, il ne recalcule jamais le passe : recalculer
        // reecrirait la courbe a chaque ouverture d'ecran, et le chiffre communique au conseil
        // ne serait plus retrouvable trois mois plus tard.
        var snapshots = await dbContext.Set<KpiSnapshot>()
            .AsNoTracking()
            .Where(snapshot => snapshot.KpiCode == definition.Code
                && snapshot.ScopeKey == scopeKey
                && snapshot.PeriodStart >= from
                && snapshot.PeriodEnd <= to)
            .OrderBy(snapshot => snapshot.PeriodStart)
            .ThenBy(snapshot => snapshot.PeriodEnd)
            .ToArrayAsync(cancellationToken);

        var unitName = await ResolveUnitNameAsync(hotelUnitCode, cancellationToken);

        var points = snapshots
            .Select(snapshot => new KpiHistoryPoint(
                snapshot.PeriodStart,
                snapshot.PeriodEnd,
                snapshot.Granularity,
                snapshot.Value,
                snapshot.Numerator,
                snapshot.Denominator,
                snapshot.Quality,
                snapshot.Status,
                snapshot.FormulaVersion,
                snapshot.CalculatedAt,
                snapshot.ClosedAt,
                snapshot.ClosedBy))
            .ToArray();

        var response = new KpiHistoryResponse(
            definition.Code,
            definition.Name,
            definition.Unit,
            definition.Polarity,
            hotelUnitCode,
            unitName,
            definition.FormulaVersion,
            points.Select(point => point.FormulaVersion).Distinct().Count() > 1,
            points);

        return ApplicationResult<KpiHistoryResponse>.Success(response);
    }

    public async Task<ApplicationResult<KpiComparisonResponse>> GetComparisonAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(query, cancellationToken);

        if (validation is not null)
        {
            return ApplicationResult<KpiComparisonResponse>.Validation(validation);
        }

        var context = await ComputeAsync(query, cancellationToken);

        var response = builder.BuildComparison(
            query,
            context.Current,
            context.Previous,
            context.Units,
            context.Thresholds,
            context.Snapshots,
            access,
            DateTimeOffset.UtcNow);

        return ApplicationResult<KpiComparisonResponse>.Success(response);
    }

    public async Task<ApplicationResult<IReadOnlyCollection<KpiAlertResponse>>> GetAlertsAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken)
    {
        // Les alertes SONT celles du tableau de bord, jamais une seconde evaluation : deux
        // moteurs d'alerte finiraient par diverger d'un seuil.
        var dashboard = await GetDashboardAsync(query, access, cancellationToken);

        if (!dashboard.Succeeded || dashboard.Value is null)
        {
            return ApplicationResult<IReadOnlyCollection<KpiAlertResponse>>.Validation(
                dashboard.Error ?? "La periode demandee est invalide.");
        }

        return ApplicationResult<IReadOnlyCollection<KpiAlertResponse>>.Success(dashboard.Value.Alerts);
    }

    /// <summary>
    /// Bornes coherentes, fenetre plafonnee, unite existante quand elle est demandee. Le plafond
    /// est celui de tout le produit (occupation du PMS, tableau de bord PDG) : les trois ecrans
    /// refusent les memes fenetres.
    /// </summary>
    private async Task<string?> ValidateAsync(KpiQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To < query.From)
        {
            return "La date de debut ne peut pas depasser la date de fin.";
        }

        if (query.To.DayNumber - query.From.DayNumber + 1 > KpiQuery.MaxWindowDays)
        {
            return $"La fenetre d'analyse ne peut pas depasser {KpiQuery.MaxWindowDays} jours.";
        }

        if (query.HotelUnitCode is not null
            && await ResolveUnitNameAsync(query.HotelUnitCode, cancellationToken) is null)
        {
            return $"L'unite {query.HotelUnitCode} n'existe pas.";
        }

        return null;
    }

    private sealed record ComputationContext(
        KpiComputation Current,
        KpiComputation Previous,
        IReadOnlyCollection<KpiUnitFact> Units,
        IReadOnlyCollection<KpiThreshold> Thresholds,
        IReadOnlyCollection<KpiSnapshot> Snapshots);

    /// <summary>
    /// Les deux passes de calcul - la periode demandee et la periode equivalente un an plus tot,
    /// chacune sur SES PROPRES faits - plus les seuils actifs et les instantanes de la periode.
    /// </summary>
    private async Task<ComputationContext> ComputeAsync(KpiQuery query, CancellationToken cancellationToken)
    {
        var period = query.ToPeriod();
        var previousPeriod = period.PreviousYear();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var currentFacts = await factLoader.LoadAsync(period.From, period.To, cancellationToken);
        var previousFacts = await factLoader.LoadAsync(previousPeriod.From, previousPeriod.To, cancellationToken);

        var thresholds = await dbContext.Set<KpiThreshold>()
            .AsNoTracking()
            .Where(threshold => threshold.IsActive)
            .ToArrayAsync(cancellationToken);

        var snapshots = await dbContext.Set<KpiSnapshot>()
            .AsNoTracking()
            .Where(snapshot => snapshot.PeriodStart == period.From && snapshot.PeriodEnd == period.To)
            .ToArrayAsync(cancellationToken);

        return new ComputationContext(
            engine.Compute(period, currentFacts, today, query.DsoMethod),
            engine.Compute(previousPeriod, previousFacts, today, query.DsoMethod),
            currentFacts.Units,
            thresholds,
            snapshots);
    }

    private async Task<string?> ResolveUnitNameAsync(string? hotelUnitCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            return null;
        }

        var normalized = Domain.Organization.HotelUnit.NormalizeCode(hotelUnitCode);

        return await dbContext.Set<Domain.Organization.HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.Code == normalized)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
