using System.Collections.Concurrent;

namespace Raqmi.Server;

public sealed record DemoSiteRecord(string Id, string Code, string Name, string Type, string? City, bool Active);

public static class DemoSiteStore
{
    private static readonly ConcurrentDictionary<string, DemoSiteRecord> Sites = new(StringComparer.Ordinal)
    {
        [DemoSites.MainSiteId] = new(DemoSites.MainSiteId, "main", "Hotel Demo — Siège", "hotel", "Alger", true),
        [DemoSites.AnnexeSiteId] = new(DemoSites.AnnexeSiteId, "annexe", "Annexe Plage", "annexe", "Tipaza", true),
    };

    public static IReadOnlyList<object> ListAll() =>
        Sites.Values.OrderBy(x => x.Name).Select(ToDto).ToList();

    public static IReadOnlyList<object> ListForUser(string userId, string roleCode)
    {
        if (string.Equals(roleCode, "admin", StringComparison.OrdinalIgnoreCase))
            return ListAll();

        var allowed = DemoUserStore.GetSiteIds(DemoData.Tenant.Id, userId);
        return Sites.Values
            .Where(s => s.Active && allowed.Contains(s.Id))
            .OrderBy(x => x.Name)
            .Select(ToDto)
            .ToList();
    }

    public static DemoSiteRecord? Get(string id) =>
        Sites.TryGetValue(id, out var site) ? site : null;

    public static int CountActive() => Sites.Values.Count(x => x.Active);

    public static DemoSiteRecord Create(string code, string name, string type, string? city, int maxSites)
    {
        if (CountActive() >= maxSites)
            throw new InvalidOperationException($"Limite de {maxSites} site(s) atteinte (licence)");

        var normalizedCode = code.Trim().ToLowerInvariant();
        if (Sites.Values.Any(x => x.Code.Equals(normalizedCode, StringComparison.Ordinal)))
            throw new InvalidOperationException("Code site déjà utilisé");

        var record = new DemoSiteRecord(
            Guid.NewGuid().ToString(),
            normalizedCode,
            name.Trim(),
            NormalizeType(type),
            string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            true);
        Sites[record.Id] = record;
        DemoAuditStore.Add("create", "sites", "Site", record.Id, $"Site créé : {record.Name}");
        return record;
    }

    public static DemoSiteRecord? Update(string id, string? name, string? type, string? city, bool? active)
    {
        if (!Sites.TryGetValue(id, out var current)) return null;

        var updated = current with
        {
            Name = name?.Trim() ?? current.Name,
            Type = type is not null ? NormalizeType(type) : current.Type,
            City = city is not null ? (string.IsNullOrWhiteSpace(city) ? null : city.Trim()) : current.City,
            Active = active ?? current.Active,
        };
        Sites[id] = updated;
        DemoAuditStore.Add("update", "sites", "Site", id, $"Site modifié : {updated.Name}");
        return updated;
    }

    private static string NormalizeType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "hotel" or "annexe" or "agency" or "branch" => type.Trim().ToLowerInvariant(),
            _ => "site",
        };

    public static object ToDto(DemoSiteRecord site) => new
    {
        id = site.Id,
        code = site.Code,
        name = site.Name,
        type = site.Type,
        city = site.City,
        active = site.Active,
    };
}
