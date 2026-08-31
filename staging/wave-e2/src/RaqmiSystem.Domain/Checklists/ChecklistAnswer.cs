namespace RaqmiSystem.Domain.Checklists;

/// <summary>
/// The three possible verdicts on one control point of a checklist run. NonConforme demands a
/// comment (a non-conformity without a stated observation is not actionable); NonApplicable
/// removes the point from the compliance score's denominator.
/// </summary>
public enum ChecklistAnswer
{
    Conforme,
    NonConforme,
    NonApplicable
}
