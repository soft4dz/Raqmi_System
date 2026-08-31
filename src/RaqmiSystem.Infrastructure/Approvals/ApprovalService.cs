using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Approvals;

/// <summary>
/// Workflow &amp; validations service. Every workflow invariant (in-order steps, role of the
/// decider, mandatory rejection comment, immutability of a closed instance, snapshot at opening)
/// lives in the domain entities; this class only orchestrates persistence, referential checks,
/// concurrency guards and auditing around them - the same division of labor as
/// <c>AccountingService</c>.
///
/// It also implements <see cref="IApprovalGate"/>, the one question consuming modules ask
/// ("may this subject proceed?"), so the gate and the workflow can never disagree on the data.
/// </summary>
public sealed class ApprovalService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IApprovalService, IApprovalGate
{
    private const string CircuitsEntity = "approvals.circuits";

    private const string InstancesEntity = "approvals.instances";

    /// <summary>
    /// Answer given when the atomic open-instance claim (see
    /// <see cref="TryClaimOpenInstanceAsync"/>) finds that the instance loaded as in-progress is
    /// no longer one: a concurrent decision closed it between our read and our write. Nothing
    /// was modified.
    /// </summary>
    private const string ConcurrentDecisionRefused =
        "This approval instance was just closed by a concurrent decision, so this change was " +
        "rolled back and nothing was modified. Reload the instance and try again.";

    public async Task<IReadOnlyCollection<ApprovalCircuitResponse>> ListCircuitsAsync(
        ApprovalSubjectType? subjectType,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ApprovalCircuit>()
            .AsNoTracking()
            .Include(circuit => circuit.Steps)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(circuit => circuit.IsActive);
        }

        if (subjectType.HasValue)
        {
            query = query.Where(circuit => circuit.SubjectType == subjectType.Value);
        }

        var circuits = await query
            .OrderBy(circuit => circuit.Code)
            .ToArrayAsync(cancellationToken);

        return circuits.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<ApprovalCircuitResponse>> GetCircuitAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var circuit = await dbContext.Set<ApprovalCircuit>()
            .AsNoTracking()
            .Include(current => current.Steps)
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (circuit is null)
        {
            return ApplicationResult<ApprovalCircuitResponse>.NotFound("Approval circuit was not found.");
        }

        return ApplicationResult<ApprovalCircuitResponse>.Success(Map(circuit));
    }

    public async Task<ApplicationResult<ApprovalCircuitResponse>> CreateCircuitAsync(
        CreateApprovalCircuitRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        ApprovalCircuit circuit;

        try
        {
            circuit = new ApprovalCircuit(request.Code, request.Label, request.SubjectType);
            circuit.ReplaceSteps(BuildSteps(request.Steps));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<ApprovalCircuitResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<ApprovalCircuit>()
            .AnyAsync(current => current.Code == circuit.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<ApprovalCircuitResponse>.Conflict("An approval circuit with this code already exists.");
        }

        circuit.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<ApprovalCircuit>().Add(circuit);

        try
        {
            await WriteAuditAsync(
                "approvals.circuit.created",
                CircuitsEntity,
                circuit.Id,
                context,
                new { circuit.Code, circuit.Label, SubjectType = circuit.SubjectType.ToString(), StepCount = circuit.Steps.Count },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same code loses the race against ux_approval_circuits_code.
            return ApplicationResult<ApprovalCircuitResponse>.Conflict("An approval circuit with this code already exists.");
        }

        return ApplicationResult<ApprovalCircuitResponse>.Success(Map(circuit));
    }

    public async Task<ApplicationResult<ApprovalCircuitResponse>> UpdateCircuitAsync(
        string code,
        UpdateApprovalCircuitRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var circuit = await dbContext.Set<ApprovalCircuit>()
            .Include(current => current.Steps)
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (circuit is null)
        {
            return ApplicationResult<ApprovalCircuitResponse>.NotFound("Approval circuit was not found.");
        }

        // Editing a circuit NEVER touches instances already opened: they snapshotted their
        // steps at opening time. Only future instances see this new configuration.
        try
        {
            circuit.UpdateDetails(request.Label);
            circuit.ReplaceSteps(BuildSteps(request.Steps));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<ApprovalCircuitResponse>.Validation(ex.Message);
        }

        circuit.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "approvals.circuit.updated",
            CircuitsEntity,
            circuit.Id,
            context,
            new { circuit.Code, circuit.Label, StepCount = circuit.Steps.Count },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ApprovalCircuitResponse>.Success(Map(circuit));
    }

    public async Task<ApplicationResult<ApprovalCircuitResponse>> SetCircuitActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var circuit = await dbContext.Set<ApprovalCircuit>()
            .Include(current => current.Steps)
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (circuit is null)
        {
            return ApplicationResult<ApprovalCircuitResponse>.NotFound("Approval circuit was not found.");
        }

        if (isActive)
        {
            // The domain guard first (at least one step), so a structurally unactivatable
            // circuit always answers 400 whatever other circuits exist. Activate only mutates
            // in memory: the Conflict return below leaves the change unsaved.
            try
            {
                circuit.Activate();
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<ApprovalCircuitResponse>.Validation(ex.Message);
            }

            // One active circuit per subject type: with several, the instance-opening path
            // could not tell which one governs a subject. This exists-check and the activation
            // are not atomic (two concurrent activations can both pass), which is tolerated
            // the same way the create/exists races are elsewhere: opening then picks
            // deterministically (lowest code) and the configuration screen shows both, so the
            // anomaly is visible and correctable rather than silent.
            var otherActive = await dbContext.Set<ApprovalCircuit>()
                .AsNoTracking()
                .AnyAsync(
                    current => current.SubjectType == circuit.SubjectType
                        && current.IsActive
                        && current.Id != circuit.Id,
                    cancellationToken);

            if (otherActive)
            {
                return ApplicationResult<ApprovalCircuitResponse>.Conflict(
                    "Another active circuit already covers this subject type. Deactivate it first.");
            }
        }
        else
        {
            circuit.Deactivate();
        }

        circuit.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "approvals.circuit.activated" : "approvals.circuit.deactivated",
            CircuitsEntity,
            circuit.Id,
            context,
            new { circuit.Code, circuit.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ApprovalCircuitResponse>.Success(Map(circuit));
    }

    public async Task<IReadOnlyCollection<ApprovalInstanceResponse>> ListInstancesAsync(
        ApprovalSubjectType? subjectType,
        string? subjectReference,
        ApprovalInstanceStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ApprovalInstance>()
            .AsNoTracking()
            .Include(instance => instance.Steps)
            .Include(instance => instance.Decisions)
            .AsQueryable();

        if (subjectType.HasValue)
        {
            query = query.Where(instance => instance.SubjectType == subjectType.Value);
        }

        var normalizedReference = NormalizeNullableReference(subjectReference);

        if (normalizedReference is not null)
        {
            query = query.Where(instance => instance.SubjectReference == normalizedReference);
        }

        if (status.HasValue)
        {
            query = query.Where(instance => instance.Status == status.Value);
        }

        // The period filters on the OPENING timestamp (CreatedAt), at UTC-day granularity:
        // instances carry no business date of their own, opening time is their only calendar.
        if (from.HasValue)
        {
            var lowerBound = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(instance => instance.CreatedAt >= lowerBound);
        }

        if (to.HasValue)
        {
            var upperBound = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(instance => instance.CreatedAt < upperBound);
        }

        // Filtered in the database, ordered in memory: the SQLite provider of the test harness
        // refuses ORDER BY on a DateTimeOffset column ("SQLite does not support expressions of
        // type 'DateTimeOffset' in ORDER BY clauses"), and this listing has no LIMIT, so sorting
        // the materialized set is exactly equivalent on both providers - the database still does
        // all the filtering, only the final ordering moved.
        var instances = await query.ToArrayAsync(cancellationToken);

        return instances
            .OrderByDescending(instance => instance.CreatedAt)
            .Select(Map)
            .ToArray();
    }

    public async Task<ApplicationResult<ApprovalInstanceResponse>> GetInstanceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var instance = await dbContext.Set<ApprovalInstance>()
            .AsNoTracking()
            .Include(current => current.Steps)
            .Include(current => current.Decisions)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (instance is null)
        {
            return ApplicationResult<ApprovalInstanceResponse>.NotFound("Approval instance was not found.");
        }

        return ApplicationResult<ApprovalInstanceResponse>.Success(Map(instance));
    }

    public async Task<IReadOnlyCollection<ApprovalInstanceResponse>> ListPendingAsync(
        IReadOnlyCollection<string> deciderRoles,
        CancellationToken cancellationToken)
    {
        if (deciderRoles is null || deciderRoles.Count == 0)
        {
            return Array.Empty<ApprovalInstanceResponse>();
        }

        // The current step is a computed notion (lowest undecided rank), not a column, so the
        // in-progress instances are loaded and the role filter applied in memory. The
        // in-progress set is by nature small (it is a work queue, not a history), so this stays
        // proportionate - and the alternative, materializing a current_rank column, would buy a
        // WHERE clause at the price of one more piece of redundant state to keep honest.
        //
        // The ordering is applied in memory for the same reason as ListInstancesAsync: the
        // SQLite provider of the test harness cannot ORDER BY a DateTimeOffset column. With no
        // LIMIT in the query, ordering the materialized set is strictly equivalent.
        var openInstances = await dbContext.Set<ApprovalInstance>()
            .AsNoTracking()
            .Include(instance => instance.Steps)
            .Include(instance => instance.Decisions)
            .Where(instance => instance.Status == ApprovalInstanceStatus.InProgress)
            .ToArrayAsync(cancellationToken);

        return openInstances
            .OrderBy(instance => instance.CreatedAt)
            .Where(instance =>
            {
                var currentStep = instance.CurrentStep;

                return currentStep is not null
                    && deciderRoles.Contains(currentStep.RequiredRole, StringComparer.OrdinalIgnoreCase);
            })
            .Select(Map)
            .ToArray();
    }

    public async Task<ApplicationResult<ApprovalInstanceResponse>> OpenInstanceAsync(
        OpenApprovalInstanceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedReference = NormalizeNullableReference(request.SubjectReference);

        if (normalizedReference is null)
        {
            return ApplicationResult<ApprovalInstanceResponse>.Validation("Subject reference is required.");
        }

        if (!Enum.IsDefined(request.SubjectType))
        {
            return ApplicationResult<ApprovalInstanceResponse>.Validation("Unknown approval subject type.");
        }

        // The governing circuit: the active one for the subject type. SetCircuitActiveAsync
        // keeps at most one active per type; should a race ever leave two, the lowest code is
        // picked deterministically rather than throwing on SingleOrDefault.
        var circuit = await dbContext.Set<ApprovalCircuit>()
            .AsNoTracking()
            .Include(current => current.Steps)
            .Where(current => current.SubjectType == request.SubjectType && current.IsActive)
            .OrderBy(current => current.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (circuit is null)
        {
            return ApplicationResult<ApprovalInstanceResponse>.Validation(
                "No active approval circuit covers this subject type. Configure and activate a circuit first.");
        }

        var alreadyOpen = await dbContext.Set<ApprovalInstance>()
            .AsNoTracking()
            .AnyAsync(
                current => current.SubjectType == request.SubjectType
                    && current.SubjectReference == normalizedReference
                    && current.Status == ApprovalInstanceStatus.InProgress,
                cancellationToken);

        if (alreadyOpen)
        {
            return ApplicationResult<ApprovalInstanceResponse>.Conflict(
                "An approval instance is already in progress for this subject.");
        }

        ApprovalInstance instance;

        try
        {
            instance = new ApprovalInstance(circuit, normalizedReference);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<ApprovalInstanceResponse>.Validation(ex.Message);
        }

        instance.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<ApprovalInstance>().Add(instance);

        try
        {
            await WriteAuditAsync(
                "approvals.instance.opened",
                InstancesEntity,
                instance.Id,
                context,
                new
                {
                    SubjectType = instance.SubjectType.ToString(),
                    instance.SubjectReference,
                    instance.CircuitCode,
                    StepCount = instance.Steps.Count
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent open for the
            // same subject loses the race against ux_approval_instances_open_subject.
            return ApplicationResult<ApprovalInstanceResponse>.Conflict(
                "An approval instance is already in progress for this subject.");
        }

        return ApplicationResult<ApprovalInstanceResponse>.Success(Map(instance));
    }

    public async Task<ApplicationResult<ApprovalInstanceResponse>> DecideAsync(
        Guid id,
        bool approved,
        string? comment,
        IReadOnlyCollection<string> deciderRoles,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Same Serializable transaction + atomic status claim as AccountingService's guarded
        // mutations: deciding reads the decisions to find the current step, so without the guard
        // two concurrent decisions (or a decision racing a rejection) could both pass the
        // in-memory status check and rewrite a closed - therefore immutable - instance.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var instance = await dbContext.Set<ApprovalInstance>()
                .Include(current => current.Steps)
                .Include(current => current.Decisions)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (instance is null)
            {
                return ApplicationResult<ApprovalInstanceResponse>.NotFound("Approval instance was not found.");
            }

            // The entity refuses this too, but the status is checked here first so that the
            // immutability of a closed instance surfaces as a 409 Conflict (the state of the
            // resource forbids the operation) rather than as a 400 among validation failures.
            if (instance.Status != ApprovalInstanceStatus.InProgress)
            {
                return ApplicationResult<ApprovalInstanceResponse>.Conflict(
                    instance.Status == ApprovalInstanceStatus.Approved
                        ? "This approval instance has already been approved and is immutable."
                        : "This approval instance has already been rejected and is immutable.");
            }

            var now = DateTimeOffset.UtcNow;

            // The status just checked in memory is re-asserted as the WHERE clause of a single
            // conditional UPDATE: only the request whose statement actually matched the row goes
            // on to record a decision. A concurrent closing makes the claim miss, and the
            // refusal is a retryable 409 rather than a silent corruption.
            if (!await TryClaimOpenInstanceAsync(instance.Id, now, cancellationToken))
            {
                return ApplicationResult<ApprovalInstanceResponse>.Conflict(ConcurrentDecisionRefused);
            }

            try
            {
                instance.Decide(context.UserName, deciderRoles, approved, comment, now);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<ApprovalInstanceResponse>.Validation(ex.Message);
            }

            instance.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                approved ? "approvals.instance.step_approved" : "approvals.instance.rejected",
                InstancesEntity,
                instance.Id,
                context,
                new
                {
                    SubjectType = instance.SubjectType.ToString(),
                    instance.SubjectReference,
                    instance.CircuitCode,
                    Rank = instance.Decisions.Max(decision => decision.Rank),
                    Approved = approved,
                    Comment = comment,
                    Status = instance.Status.ToString()
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ApprovalInstanceResponse>.Success(Map(instance));
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // ux_approval_decisions_instance_rank: a concurrent request decided this very step
            // first. One decision per step is the whole point of that index.
            return ApplicationResult<ApprovalInstanceResponse>.Conflict(ConcurrentDecisionRefused);
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ApprovalInstanceResponse>.Conflict(ConcurrentDecisionRefused);
        }
    }

    /// <summary>
    /// The gate consuming modules call (today: the treasury payment-order approval).
    /// Backward-compatible on purpose: NO active circuit for the type means NO blocking - an
    /// installation that never configured approvals behaves exactly as before this module
    /// existed. With an active circuit, only an APPROVED instance for the reference opens the
    /// gate; in-progress, rejected or absent instances keep it shut.
    /// </summary>
    public async Task<ApplicationResult<bool>> IsApprovedAsync(
        ApprovalSubjectType type,
        string reference,
        CancellationToken cancellationToken)
    {
        var normalizedReference = NormalizeNullableReference(reference);

        if (normalizedReference is null)
        {
            return ApplicationResult<bool>.Validation("Subject reference is required.");
        }

        var covered = await dbContext.Set<ApprovalCircuit>()
            .AsNoTracking()
            .AnyAsync(circuit => circuit.SubjectType == type && circuit.IsActive, cancellationToken);

        if (!covered)
        {
            return ApplicationResult<bool>.Success(true);
        }

        var hasApprovedInstance = await dbContext.Set<ApprovalInstance>()
            .AsNoTracking()
            .AnyAsync(
                instance => instance.SubjectType == type
                    && instance.SubjectReference == normalizedReference
                    && instance.Status == ApprovalInstanceStatus.Approved,
                cancellationToken);

        return ApplicationResult<bool>.Success(hasApprovedInstance);
    }

    /// <summary>
    /// Atomic form of "this instance is still in progress". The invariant travels as the WHERE
    /// clause of one conditional UPDATE on the instance's own row, evaluated by the database at
    /// the instant the row is claimed - the claim-in-one-statement pattern of
    /// <c>AccountingService.TryClaimDraftEntryAsync</c>. The single column it writes,
    /// <c>UpdatedAt</c>, is one the caller's mutation stamps anyway with the same timestamp: the
    /// claim adds no state of its own, it only needs to be a write so the row is claimed, not
    /// merely read.
    /// </summary>
    private async Task<bool> TryClaimOpenInstanceAsync(
        Guid instanceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<ApprovalInstance>()
            .Where(current => current.Id == instanceId && current.Status == ApprovalInstanceStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    private static List<ApprovalStep> BuildSteps(IReadOnlyCollection<ApprovalStepRequest>? requests)
    {
        if (requests is null)
        {
            return new List<ApprovalStep>();
        }

        return requests
            .Select(step => new ApprovalStep(step.Label, step.RequiredRole))
            .ToList();
    }

    private static ApprovalCircuitResponse Map(ApprovalCircuit circuit)
    {
        var steps = circuit.Steps
            .OrderBy(step => step.Rank)
            .Select(step => new ApprovalStepResponse(step.Rank, step.Label, step.RequiredRole))
            .ToArray();

        return new ApprovalCircuitResponse(
            circuit.Id,
            circuit.Code,
            circuit.Label,
            circuit.SubjectType,
            circuit.IsActive,
            steps,
            circuit.CreatedAt,
            circuit.CreatedBy,
            circuit.UpdatedAt,
            circuit.UpdatedBy);
    }

    private static ApprovalInstanceResponse Map(ApprovalInstance instance)
    {
        var decidedRanks = instance.Decisions
            .Select(decision => decision.Rank)
            .ToHashSet();

        var steps = instance.Steps
            .OrderBy(step => step.Rank)
            .Select(step => new ApprovalInstanceStepResponse(
                step.Rank,
                step.Label,
                step.RequiredRole,
                decidedRanks.Contains(step.Rank)))
            .ToArray();

        var decisions = instance.Decisions
            .OrderBy(decision => decision.Rank)
            .Select(decision => new ApprovalDecisionResponse(
                decision.Rank,
                decision.StepLabel,
                decision.DecidedBy,
                decision.Approved,
                decision.Comment,
                decision.DecidedAt))
            .ToArray();

        var currentStep = instance.CurrentStep;

        return new ApprovalInstanceResponse(
            instance.Id,
            instance.SubjectType,
            instance.SubjectReference,
            instance.CircuitCode,
            instance.CircuitLabel,
            instance.Status,
            currentStep?.Rank,
            currentStep?.Label,
            currentStep?.RequiredRole,
            steps,
            decisions,
            instance.ClosedAt,
            instance.ClosedBy,
            instance.CreatedAt,
            instance.CreatedBy,
            instance.UpdatedAt,
            instance.UpdatedBy);
    }

    /// <summary>
    /// Lookup normalization for a code coming from a route: a malformed code must produce a
    /// clean 404 (nothing matches) rather than an exception, while a code being CREATED goes
    /// through the strict normalization in the entity's constructor.
    /// </summary>
    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableReference(string? reference)
    {
        return string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the
    /// audit row), so this call is usually a no-op - it exists so persistence never silently
    /// depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
