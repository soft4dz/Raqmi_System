using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Tariffs;

public interface ITariffResolutionService
{
    // Resout le tarif d'une nuit pour une unite + type de chambre + date, avec
    // application de la convention du client quand customerCode est fourni.
    Task<ApplicationResult<ResolvedNightlyRate>> ResolveAsync(
        string hotelUnitCode, string roomTypeCode, DateOnly night,
        string? customerCode, CancellationToken cancellationToken);
}
