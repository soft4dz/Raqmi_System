namespace Raqmi.Server;

public sealed record DemoRoleRecord(string Code, string Label, bool IsSystem, List<string> Permissions);

public static class DemoRoleStore
{
    private static readonly List<DemoRoleRecord> Roles =
    [
        new("admin", "Administrateur", true, ["*"]),
        new("manager", "Responsable", true,
        [
            "administration:read", "sites:read", "finance:read", "finance:write",
            "hr:read", "hr:write", "stocks:read", "stocks:write", "ged:read",
        ]),
        new("user", "Utilisateur", true,
        [
            "finance:read", "hr:read", "stocks:read", "ged:read",
        ]),
    ];

    public static IReadOnlyList<object> List() =>
        Roles.OrderBy(r => r.Label).Select(r => new
        {
            code = r.Code,
            label = r.Label,
            isSystem = r.IsSystem,
            permissions = r.Permissions,
        }).Cast<object>().ToList();

    public static DemoRoleRecord? Get(string code) =>
        Roles.FirstOrDefault(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static DemoRoleRecord Create(string code, string label, List<string> permissions)
    {
        var normalized = code.Trim().ToLowerInvariant();
        if (Roles.Any(r => r.Code.Equals(normalized, StringComparison.Ordinal)))
            throw new InvalidOperationException("Code rôle déjà utilisé");
        if (string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("Libellé requis");

        var role = new DemoRoleRecord(normalized, label.Trim(), false, permissions.Distinct(StringComparer.Ordinal).ToList());
        Roles.Add(role);
        DemoAuditStore.Add("create", "administration", "Role", role.Code, $"Rôle créé : {role.Label}");
        return role;
    }

    public static DemoRoleRecord? Update(string code, string? label, List<string>? permissions)
    {
        var index = Roles.FindIndex(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        var current = Roles[index];
        if (current.IsSystem && permissions is not null)
            throw new InvalidOperationException("Les permissions des rôles système ne sont pas modifiables");

        var updated = current with
        {
            Label = label?.Trim() ?? current.Label,
            Permissions = permissions ?? current.Permissions,
        };
        Roles[index] = updated;
        DemoAuditStore.Add("update", "administration", "Role", code, $"Rôle modifié : {updated.Label}");
        return updated;
    }

    public static bool Delete(string code)
    {
        var role = Roles.FirstOrDefault(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (role is null) return false;
        if (role.IsSystem)
            throw new InvalidOperationException("Impossible de supprimer un rôle système");
        Roles.Remove(role);
        DemoAuditStore.Add("delete", "administration", "Role", code, $"Rôle supprimé : {role.Label}");
        return true;
    }

    public static bool HasPermission(string roleCode, string module, string action)
    {
        var role = Get(roleCode);
        if (role is null) return false;
        if (role.Permissions.Contains("*")) return true;
        return role.Permissions.Contains($"{module}:{action}", StringComparer.OrdinalIgnoreCase)
            || role.Permissions.Contains($"{module}:*", StringComparer.OrdinalIgnoreCase);
    }
}
