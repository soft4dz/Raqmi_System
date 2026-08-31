namespace RaqmiSystem.Domain.Complaints;

/// <summary>
/// Channel through which a complaint reached the establishment. Member names are the
/// business vocabulary (French, unaccented) because they are persisted as strings and
/// exchanged with the API; the screen translates them once for display.
/// </summary>
public enum ComplaintChannel
{
    SurPlace,
    Telephone,
    Email,
    ReseauxSociaux,
    Autre
}
