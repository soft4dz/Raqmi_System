using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Module 24.2 - Dashboard PDG. The group-wide reading of the direction: every hotel unit at
/// once, over one period, compared with the equivalent period one year earlier. Pure
/// aggregation over the other modules' data - this service owns no table of its own and never
/// writes anything.
/// </summary>
public interface IGroupDashboardService
{
    Task<ApplicationResult<GroupDashboardResponse>> GetGroupDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
