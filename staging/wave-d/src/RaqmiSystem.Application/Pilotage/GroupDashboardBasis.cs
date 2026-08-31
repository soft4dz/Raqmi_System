namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// What each family of figures counts, in the server's own words, carried inside the payload
/// itself - the same honesty device as AgingBalanceResponse.AgingBasis: a consumer that
/// renders these figures can (and the desktop view does) show the reader exactly what they
/// are, and a rule change on the server travels with the numbers it changes.
/// </summary>
public sealed record GroupDashboardBasis(
    string Revenue,
    string Receipts,
    string Receivables,
    string Occupancy,
    string Closing);
