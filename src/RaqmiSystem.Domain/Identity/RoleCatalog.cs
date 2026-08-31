namespace RaqmiSystem.Domain.Identity;

public static class RoleCatalog
{
    public const string SystemAdministrator = "system.administrator";
    public const string Direction = "direction";
    public const string ExploitationControl = "exploitation.control";
    public const string UnitManager = "unit.manager";
    public const string Cashier = "cashier";
    public const string Reader = "reader";

    /// <summary>
    /// The HR role. It is separate from every operating role on purpose: the HR module holds the
    /// personal data protected by law 18-07 (national identity and social security numbers, bank
    /// accounts, dependants) and the payroll figures, and those must not come bundled with a
    /// front-desk or accounting profile. It is deliberately absent from
    /// <see cref="ApprovalDeciderRoles"/> - it never receives approvals.decide.
    /// </summary>
    public const string HrManager = "hr.manager";

    /// <summary>
    /// The roles that carry <see cref="PermissionCatalog.ApprovalsDecide"/> - i.e. the ONLY
    /// roles an approval step may ever require.
    ///
    /// This is not a stylistic list: a step demanding a role that never receives
    /// approvals.decide is UNDECIDABLE FOR LIFE. Its holder is refused by the authorization
    /// policy (403) while every holder of approvals.decide fails the role check inside
    /// ApprovalInstance.Decide - and because the step is frozen by the opening-time snapshot,
    /// an instance already opened on such a circuit can never be closed. The rule therefore
    /// lives here, next to the role names, and is enforced by the DOMAIN
    /// (<see cref="Approvals.ApprovalStep"/>), which protects the API and the desktop alike
    /// instead of relying on a screen offering the right choices.
    ///
    /// system.administrator is part of the list because it holds every permission of the
    /// catalog, approvals.decide included (SecuritySeeder grants it PermissionCatalog.All).
    /// SecuritySeederTests pins the equality between this list and the roles actually granted
    /// approvals.decide, so the two can never silently drift apart.
    /// </summary>
    public static readonly IReadOnlyCollection<string> ApprovalDeciderRoles = new[]
    {
        SystemAdministrator,
        Direction,
        ExploitationControl,
        UnitManager
    };
}
