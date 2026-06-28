namespace Raqmi.Server;

public sealed record DemoAuditEntry(
    string Id,
    string? UserId,
    string Action,
    string? ModuleCode,
    string? EntityType,
    string? EntityId,
    string Description,
    DateTime CreatedAt);

public static class DemoAuditStore
{
    private static readonly List<DemoAuditEntry> Entries = [];
    private static bool _seeded;

    private static void EnsureSeed()
    {
        if (_seeded) return;
        _seeded = true;
        Add("login", "administration", "User", DemoData.User.Id, "Connexion administrateur", DemoData.User.Id);
        Add("create", "sites", "Site", DemoSites.MainSiteId, "Site demo initialisé", DemoData.User.Id);
    }

    public static void Add(
        string action,
        string? moduleCode,
        string? entityType,
        string? entityId,
        string description,
        string? userId = null)
    {
        EnsureSeed();
        Entries.Insert(0, new DemoAuditEntry(
            Guid.NewGuid().ToString(),
            userId ?? DemoData.User.Id,
            action,
            moduleCode,
            entityType,
            entityId,
            description,
            DateTime.UtcNow));
        if (Entries.Count > 500) Entries.RemoveAt(Entries.Count - 1);
    }

    public static IReadOnlyList<object> List(int limit = 100, string? action = null, string? moduleCode = null, string? query = null)
    {
        EnsureSeed();
        IEnumerable<DemoAuditEntry> items = Entries;
        if (!string.IsNullOrWhiteSpace(action))
            items = items.Where(e => e.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(moduleCode))
            items = items.Where(e => string.Equals(e.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
            items = items.Where(e => e.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        return items.Take(limit).Select(e => new
        {
            id = e.Id,
            userId = e.UserId,
            action = e.Action,
            moduleCode = e.ModuleCode,
            entityType = e.EntityType,
            entityId = e.EntityId,
            description = e.Description,
            createdAt = e.CreatedAt,
        }).Cast<object>().ToList();
    }
}
