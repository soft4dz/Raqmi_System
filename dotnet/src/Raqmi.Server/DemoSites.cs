namespace Raqmi.Server;

public static class DemoSites
{
    public const string MainSiteId = "demo-site-001";
    public const string AnnexeSiteId = "demo-site-002";

    public static IReadOnlyList<object> All { get; } =
    [
        new { id = MainSiteId, code = "main", name = "Hotel Demo — Siège", city = "Alger", active = true },
        new { id = AnnexeSiteId, code = "annexe", name = "Annexe Plage", city = "Tipaza", active = true },
    ];
}
