using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Deterministic stand-in for the tariff module's <see cref="ITariffResolutionService"/>: the
/// lodging module only CONSUMES that contract, so its tests pin the resolved rate instead of
/// depending on tariff data. Set <see cref="NextResult"/> to script one failure (or a specific
/// success) for the next resolution; it then reverts to the fixed success.
/// </summary>
internal sealed class StubTariffResolutionService(
    decimal amount = 10_000.00m,
    string ratePlanCode = "STD") : ITariffResolutionService
{
    public ApplicationResult<ResolvedNightlyRate>? NextResult { get; set; }

    public int ResolveCallCount { get; private set; }

    public Task<ApplicationResult<ResolvedNightlyRate>> ResolveAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly night,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        ResolveCallCount++;

        var result = NextResult
            ?? ApplicationResult<ResolvedNightlyRate>.Success(
                new ResolvedNightlyRate(amount, ratePlanCode, null, null));

        NextResult = null;

        return Task.FromResult(result);
    }
}
