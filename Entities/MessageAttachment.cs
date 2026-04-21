namespace SRAAS.Api.Entities;

public class MessageAttachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid OrgId { get; set; }
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public int? FileSizeKb { get; set; }
    public string StorageKey { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Message Message { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
