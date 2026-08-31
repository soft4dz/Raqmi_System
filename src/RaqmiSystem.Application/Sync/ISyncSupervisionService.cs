using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Sync;

/// <summary>
/// Supervision of the deployed workstations. This service does NOT synchronise anything and must
/// never grow into something that does: every workstation writes into the same PostgreSQL
/// database through the same API, so there is no divergent state to reconcile. Its only job is to
/// make the fleet and its failures visible after the fact.
/// </summary>
public interface ISyncSupervisionService
{
    /// <summary>
    /// Records a contact from a workstation, creating its registry row on first sight. Callable
    /// by ANY authenticated user: a registry that only administrators could feed would be empty
    /// of exactly the front-desk machines it is meant to watch.
    /// </summary>
    Task<ApplicationResult<WorkstationResponse>> HeartbeatAsync(
        WorkstationHeartbeatRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a batch of client-side failures, ignoring entries already known. Returns how many
    /// rows were actually added, so a resent batch reports zero rather than pretending to work.
    /// </summary>
    Task<ApplicationResult<int>> ReportFailuresAsync(
        ReportWorkstationFailuresRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the registry. By default only workstations seen in the last 30 days are returned;
    /// <paramref name="includeAllKnown"/> lifts that filter. There is no delete: an inventory
    /// that can be silently pruned is not an inventory.
    /// </summary>
    Task<ApplicationResult<WorkstationRegistryResponse>> GetRegistryAsync(
        bool includeAllKnown,
        CancellationToken cancellationToken);

    /// <summary>Reads the most recent reported failures, newest first.</summary>
    Task<ApplicationResult<IReadOnlyCollection<WorkstationFailureResponse>>> GetFailuresAsync(
        int maxItems,
        CancellationToken cancellationToken);
}
