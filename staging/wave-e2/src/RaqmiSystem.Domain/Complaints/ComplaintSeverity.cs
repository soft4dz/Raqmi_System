namespace RaqmiSystem.Domain.Complaints;

/// <summary>
/// Severity assessed at registration. Critique drives the "share of critical
/// complaints" indicator of the quality report and the open-critical tile.
/// </summary>
public enum ComplaintSeverity
{
    Mineure,
    Majeure,
    Critique
}
