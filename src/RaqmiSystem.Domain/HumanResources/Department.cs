using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// An organisational department (reception, housekeeping, food and beverage, administration...).
///
/// Departments are GROUP-WIDE reference data, not per-unit: the same "housekeeping" department
/// exists in every hotel of the group, and a payroll or headcount report that had to reconcile
/// twelve unit-local spellings of the same department would be worthless. The unit an employee
/// belongs to is carried by the employee, not by the department.
/// </summary>
public sealed class Department : AuditableEntity
{
    private Department()
    {
    }

    public Department(string code, string label)
    {
        Code = NormalizeCode(code);
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label)
    {
        Label = HumanResourcesText.Require(label, nameof(label), 160);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public static string NormalizeCode(string value)
    {
        return HumanResourcesText.Require(value, nameof(value), 40).ToUpperInvariant();
    }
}
