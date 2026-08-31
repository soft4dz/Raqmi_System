namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Read-only aggregation service behind the DEC cockpit (module Pilotage). No writes, no new
/// tables: it only reads the existing modules' data and hands them to
/// <see cref="DecCockpitCalculator"/>.
/// </summary>
public interface IDecCockpitService
{
    /// <summary>
    /// Builds the cockpit for the given business date (normally today): work queues,
    /// per-unit health for yesterday/today, and workload indicators.
    /// </summary>
    Task<DecCockpitResponse> GetCockpitAsync(DateOnly date, CancellationToken cancellationToken);
}
