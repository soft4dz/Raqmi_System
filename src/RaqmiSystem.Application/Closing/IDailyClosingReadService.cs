namespace RaqmiSystem.Application.Closing;

/// <summary>
/// Lightweight read-side contract for other modules that only need to know whether a
/// business day is currently locked for a hotel unit. The revenue module (and any future
/// module writing day-scoped operational data) should inject this interface and refuse
/// create/update/submit operations when <see cref="IsClosedAsync"/> returns true.
/// </summary>
public interface IDailyClosingReadService
{
    /// <summary>
    /// Returns true when a closing exists for this business date and hotel unit and its
    /// status is currently Closed (a reopened day is not considered locked).
    /// </summary>
    Task<bool> IsClosedAsync(
        DateOnly businessDate,
        string hotelUnitCode,
        CancellationToken cancellationToken);
}
