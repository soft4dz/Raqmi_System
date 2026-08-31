using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// A job position (receptionist, night auditor, head of housekeeping...) belonging to one
/// <see cref="Department"/>.
///
/// <see cref="MinimumGrossSalary"/> is the floor the position was defined with. It is a
/// CONTROL value, never applied automatically: a contract below it is refused by
/// <see cref="EmploymentContract"/> so an offer cannot silently undercut the grid, but raising
/// the floor later never rewrites contracts already signed.
/// </summary>
public sealed class Position : AuditableEntity
{
    private Position()
    {
    }

    public Position(string code, string label, string departmentCode, decimal minimumGrossSalary)
    {
        Code = NormalizeCode(code);
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        DepartmentCode = Department.NormalizeCode(departmentCode);
        MinimumGrossSalary = RequireSalary(minimumGrossSalary, nameof(minimumGrossSalary));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string DepartmentCode { get; private set; } = string.Empty;

    public decimal MinimumGrossSalary { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, string departmentCode, decimal minimumGrossSalary)
    {
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        DepartmentCode = Department.NormalizeCode(departmentCode);
        MinimumGrossSalary = RequireSalary(minimumGrossSalary, nameof(minimumGrossSalary));
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public static string NormalizeCode(string value)
    {
        return HumanResourcesText.Require(value, nameof(value), 40).ToUpperInvariant();
    }

    private static decimal RequireSalary(decimal value, string argumentName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(argumentName, "A minimum gross salary cannot be negative.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
