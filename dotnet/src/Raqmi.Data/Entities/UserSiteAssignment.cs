namespace Raqmi.Data.Entities;

public class UserSiteAssignment
{
    public string UserId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public Site Site { get; set; } = null!;
}
