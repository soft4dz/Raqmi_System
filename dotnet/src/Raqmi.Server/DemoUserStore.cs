namespace Raqmi.Server;

public sealed record DemoUserRecord(
    string Id,
    string TenantId,
    string Email,
    string FullName,
    string RoleCode,
    string Password,
    bool Active,
    List<string> SiteIds);

public static class DemoUserStore
{
    private static readonly object Gate = new();
    private static readonly List<DemoUserRecord> Users =
    [
        new(DemoData.User.Id, DemoData.User.TenantId, DemoData.User.Email, DemoData.User.FullName, DemoData.User.RoleCode, DemoData.User.Password, true,
            [DemoSites.MainSiteId, DemoSites.AnnexeSiteId]),
        new("demo-user-002", DemoData.Tenant.Id, "manager@demo.raqmi.local", "Responsable Demo", "manager", "demo1234", true,
            [DemoSites.MainSiteId, DemoSites.AnnexeSiteId]),
        new("demo-user-003", DemoData.Tenant.Id, "user@demo.raqmi.local", "Utilisateur Demo", "user", "demo1234", true,
            [DemoSites.AnnexeSiteId]),
    ];

    public static DemoUserRecord? FindByEmail(string tenantId, string email) =>
        Users.FirstOrDefault(u => u.TenantId == tenantId && u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<object> ListForTenant(string tenantId, IReadOnlyList<string>? restrictToSiteIds = null) =>
        Users
            .Where(u => u.TenantId == tenantId)
            .Where(u => restrictToSiteIds is null || u.SiteIds.Any(restrictToSiteIds.Contains))
            .Select(ToDto)
            .ToList();

    public static DemoUserRecord? Find(string tenantId, string id) =>
        Users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == id);

    public static IReadOnlyList<string> GetSiteIds(string tenantId, string userId)
    {
        var user = Find(tenantId, userId);
        return user?.SiteIds ?? [];
    }

    public static DemoUserRecord Create(string tenantId, string email, string fullName, string roleCode, string password, IReadOnlyList<string>? siteIds)
    {
        lock (Gate)
        {
            if (Users.Any(u => u.TenantId == tenantId && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Email déjà utilisé");

            var user = new DemoUserRecord(
                Guid.NewGuid().ToString(),
                tenantId,
                email.Trim().ToLowerInvariant(),
                fullName.Trim(),
                roleCode.Trim().ToLowerInvariant(),
                password,
                true,
                NormalizeSiteIds(siteIds));
            Users.Add(user);
            DemoAuditStore.Add("create", "administration", "User", user.Id, $"Utilisateur créé : {user.Email}");
            return user;
        }
    }

    public static DemoUserRecord? Update(string tenantId, string id, string? fullName, string? roleCode, bool? active, IReadOnlyList<string>? siteIds, string? password = null)
    {
        lock (Gate)
        {
            var index = Users.FindIndex(u => u.TenantId == tenantId && u.Id == id);
            if (index < 0) return null;

            var current = Users[index];
            var updated = current with
            {
                FullName = fullName ?? current.FullName,
                RoleCode = roleCode ?? current.RoleCode,
                Active = active ?? current.Active,
                SiteIds = siteIds is not null ? NormalizeSiteIds(siteIds) : current.SiteIds,
                Password = string.IsNullOrWhiteSpace(password) ? current.Password : password,
            };
            Users[index] = updated;
            DemoAuditStore.Add("update", "administration", "User", id, $"Utilisateur modifié : {updated.FullName}");
            return updated;
        }
    }

    private static List<string> NormalizeSiteIds(IReadOnlyList<string>? siteIds)
    {
        if (siteIds is null || siteIds.Count == 0)
            return [DemoSites.MainSiteId];

        return siteIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
    }

    public static object ToDto(DemoUserRecord user) => new
    {
        id = user.Id,
        email = user.Email,
        fullName = user.FullName,
        roleCode = user.RoleCode,
        active = user.Active,
        siteIds = user.SiteIds,
    };
}
