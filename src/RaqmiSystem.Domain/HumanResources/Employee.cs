using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// An employee record - the personal file the whole HR module hangs off.
///
/// PERSONAL DATA. This entity carries the most sensitive data set of the ERP: national identity
/// number, social security number, bank account, dependants. Under Algerian law 18-07 those are
/// personal data subject to purpose limitation, so two rules apply here and are enforced
/// elsewhere in the module: every read and write of this entity goes through an authorised
/// endpoint and is audited, and biometric data is never stored - a time clock contributes a
/// badge identifier and timestamps, nothing else (see <see cref="BadgeId"/>).
///
/// <see cref="DependentChildren"/> is not administrative decoration: it increases the IRG
/// abatement and therefore changes the net pay, which is why it lives on the employee rather
/// than being retyped for each payslip.
/// </summary>
public sealed class Employee : AuditableEntity
{
    private Employee()
    {
    }

    public Employee(
        string employeeNumber,
        string firstName,
        string lastName,
        string hotelUnitCode,
        string positionCode,
        DateOnly hireDate)
    {
        EmployeeNumber = NormalizeEmployeeNumber(employeeNumber);
        FirstName = HumanResourcesText.Require(firstName, nameof(firstName), 120);
        LastName = HumanResourcesText.Require(lastName, nameof(lastName), 120);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        PositionCode = Position.NormalizeCode(positionCode);
        HireDate = hireDate;
        Status = EmployeeStatus.Active;
    }

    /// <summary>Payroll identifier of the employee, unique across the group.</summary>
    public string EmployeeNumber { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string PositionCode { get; private set; } = string.Empty;

    public DateOnly HireDate { get; private set; }

    public DateOnly? TerminationDate { get; private set; }

    public EmployeeStatus Status { get; private set; } = EmployeeStatus.Active;

    /// <summary>NIN - national identity number, reported on DADS-U and ANEM declarations.</summary>
    public string? NationalIdentityNumber { get; private set; }

    /// <summary>NSS - social security number, the key CNAS declarations are filed under.</summary>
    public string? SocialSecurityNumber { get; private set; }

    /// <summary>RIB - bank account the net pay is transferred to.</summary>
    public string? BankAccountNumber { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>
    /// Time-clock badge identifier. Deliberately the ONLY link to the attendance hardware: it is
    /// an opaque identifier, never a biometric template, which stays on the device.
    /// </summary>
    public string? BadgeId { get; private set; }

    public int DependentChildren { get; private set; }

    /// <summary>
    /// True when the employee should be picked up by a pre-payroll run for the given period:
    /// hired on or before the end of the period, and not gone before it starts.
    ///
    /// A TERMINATED employee is still payable for the month they left - the days actually worked
    /// before the departure have to be paid, and that final payslip is what the settlement is
    /// built on. Only the months AFTER the departure are excluded. A suspended employee is not
    /// payable at all: the suspension is precisely what interrupts the pay.
    ///
    /// Keeping the rule here means the payroll service and any future report answer it the same
    /// way.
    /// </summary>
    public bool IsPayableFor(PayrollMonth period)
    {
        if (Status == EmployeeStatus.Suspended)
        {
            return false;
        }

        if (HireDate > period.LastDay)
        {
            return false;
        }

        return TerminationDate is null || TerminationDate >= period.FirstDay;
    }

    public void UpdateIdentity(string firstName, string lastName, string? email, string? phone)
    {
        FirstName = HumanResourcesText.Require(firstName, nameof(firstName), 120);
        LastName = HumanResourcesText.Require(lastName, nameof(lastName), 120);
        Email = HumanResourcesText.Optional(email, nameof(email), 200);
        Phone = HumanResourcesText.Optional(phone, nameof(phone), 40);
    }

    public void UpdateLegalIdentifiers(
        string? nationalIdentityNumber,
        string? socialSecurityNumber,
        string? bankAccountNumber)
    {
        NationalIdentityNumber = HumanResourcesText.Optional(
            nationalIdentityNumber,
            nameof(nationalIdentityNumber),
            40);

        SocialSecurityNumber = HumanResourcesText.Optional(
            socialSecurityNumber,
            nameof(socialSecurityNumber),
            40);

        BankAccountNumber = HumanResourcesText.Optional(
            bankAccountNumber,
            nameof(bankAccountNumber),
            40);
    }

    public void UpdateAssignment(string hotelUnitCode, string positionCode)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        PositionCode = Position.NormalizeCode(positionCode);
    }

    public void SetBadge(string? badgeId)
    {
        BadgeId = HumanResourcesText.Optional(badgeId, nameof(badgeId), 60);
    }

    public void SetDependentChildren(int count)
    {
        // The upper bound is a typo guard, not a family-size judgement: each child raises the IRG
        // abatement, so a stray extra digit would quietly cancel someone's income tax.
        if (count is < 0 or > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Dependent children must be between 0 and 30.");
        }

        DependentChildren = count;
    }

    public void Suspend()
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new InvalidOperationException("A terminated employee cannot be suspended.");
        }

        Status = EmployeeStatus.Suspended;
    }

    public void Reactivate()
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new InvalidOperationException(
                "A terminated employee cannot be reactivated. Create a new employee record instead.");
        }

        Status = EmployeeStatus.Active;
    }

    public void Terminate(DateOnly terminationDate)
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new InvalidOperationException("The employee is already terminated.");
        }

        if (terminationDate < HireDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminationDate),
                "A termination date cannot precede the hire date.");
        }

        TerminationDate = terminationDate;
        Status = EmployeeStatus.Terminated;
    }

    public static string NormalizeEmployeeNumber(string value)
    {
        return HumanResourcesText.Require(value, nameof(value), 40).ToUpperInvariant();
    }
}
