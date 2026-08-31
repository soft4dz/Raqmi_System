namespace RaqmiSystem.Domain.Complaints;

/// <summary>
/// Lifecycle of a complaint. Received -> UnderReview -> Resolved -> Closed, with
/// Resolved reachable straight from Received (a front-desk complaint can be settled on
/// the spot). Closed is terminal and immutable: the record becomes part of the quality
/// history and no transition leaves it.
/// </summary>
public enum ComplaintStatus
{
    Received,
    UnderReview,
    Resolved,
    Closed
}
