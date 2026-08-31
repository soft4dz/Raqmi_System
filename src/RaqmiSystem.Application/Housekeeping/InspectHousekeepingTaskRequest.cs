namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// Supervisor verdict on a finished room. Notes are mandatory when Accepted is false: a refusal
/// nobody can explain teaches the attendant nothing.
/// </summary>
public sealed record InspectHousekeepingTaskRequest(bool Accepted, string? Notes = null);
