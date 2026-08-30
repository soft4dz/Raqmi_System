using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Deterministic stand-in for the tariff module's <see cref="ITariffResolutionService"/>: the
/// lodging module only CONSUMES that contract, so its tests pin the resolved rate instead of
/// depending on tariff data. Set <see cref="NextResult"/> to script one failure (or a specific
/// success) for the next resolution (it then reverts to the fixed success), or
/// <see cref="RateByNight"/> to script the resolution night by night (variable rates across
/// rate periods, coverage holes on specific nights, per-customer conventions - the function
/// receives the night and the customer code).
/// </summary>
internal sealed class StubTariffResolutionService(
    decimal amount = 10_000.00m,
    string ratePlanCode = "STD") : ITariffResolutionService
{
    public ApplicationResult<ResolvedNightlyRate>? NextResult { get; set; }

    public Func<DateOnly, string?, ApplicationResult<ResolvedNightlyRate>>? RateByNight { get; set; }

    public int ResolveCallCount { get; private set; }

    public Task<ApplicationResult<ResolvedNightlyRate>> ResolveAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly night,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        ResolveCallCount++;

        if (NextResult is not null)
        {
            var scripted = NextResult;
            NextResult = null;
            return Task.FromResult(scripted);
        }

        if (RateByNight is not null)
        {
            return Task.FromResult(RateByNight(night, customerCode));
        }

        return Task.FromResult(ApplicationResult<ResolvedNightlyRate>.Success(
            new ResolvedNightlyRate(amount, ratePlanCode, null, null)));
    }
}
