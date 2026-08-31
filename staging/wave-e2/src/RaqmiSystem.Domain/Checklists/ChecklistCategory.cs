namespace RaqmiSystem.Domain.Checklists;

/// <summary>
/// Functional family of a checklist template. The category classifies the template for
/// filtering and reporting; it never changes the mechanics of a run.
/// </summary>
public enum ChecklistCategory
{
    OuvertureJournee,
    ClotureJournee,
    Hygiene,
    Securite,
    Audit,
    Autre
}
