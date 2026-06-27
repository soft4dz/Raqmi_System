namespace Raqmi.Data.Entities;

public class FileObject : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string? SiteId { get; set; }
    public string StorageDriver { get; set; } = "LOCAL";
    public string? Bucket { get; set; }
    public string Path { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public string? ModuleCode { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
