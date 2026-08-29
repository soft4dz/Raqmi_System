using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Settings;

public interface IApplicationSettingsService
{
    /// <summary>
    /// Always returns the settings the installation currently runs with - never "not found".
    /// While the singleton row is absent, the defaults are returned as-is (a read writes
    /// nothing); the first update materializes the row.
    /// </summary>
    Task<ApplicationSettingsResponse> GetAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<ApplicationSettingsResponse>> UpdateAsync(
        UpdateApplicationSettingsRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
