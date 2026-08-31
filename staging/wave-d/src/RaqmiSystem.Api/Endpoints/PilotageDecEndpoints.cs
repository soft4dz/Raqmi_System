using System.Globalization;
using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// DEC cockpit endpoints (module Pilotage). Read-only: the cockpit aggregates the existing
/// modules' data and never writes anything. The permission is the EXISTING dashboard.read key
/// (already seeded for direction, exploitation.control, unit.manager and reader) - written as
/// a literal string here; the integrator will substitute the PermissionCatalog constant.
/// NOTE for the integrator: the PDG dashboard agent maps its own endpoints under the same
/// /pilotage group from its own file; merge the two Map calls in Program.cs.
/// </summary>
internal static class PilotageDecEndpoints
{
    public static RouteGroupBuilder MapPilotageDecEndpoints(this RouteGroupBuilder api)
    {
        var pilotage = api.MapGroup("/pilotage")
            .WithTags("Pilotage DEC");

        pilotage.MapGet("/dec-cockpit", async (
            string? date,
            IDecCockpitService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseCockpitDate(date, out var cockpitDate, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.GetCockpitAsync(cockpitDate, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization("dashboard.read");

        return api;
    }

    private static bool TryParseCockpitDate(string? date, out DateOnly cockpitDate, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(date))
        {
            cockpitDate = DateOnly.FromDateTime(DateTime.UtcNow);
            return true;
        }

        if (DateOnly.TryParse(date.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out cockpitDate))
        {
            return true;
        }

        error = "The date must be a valid date (yyyy-MM-dd).";
        return false;
    }
}
