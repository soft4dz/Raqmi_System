namespace RaqmiSystem.Domain.Checklists;

/// <summary>
/// Lifecycle of a checklist run: answered item by item while InProgress, then immutable once
/// Completed (the completion freezes the compliance score for good).
/// </summary>
public enum ChecklistRunStatus
{
    InProgress,
    Completed
}
