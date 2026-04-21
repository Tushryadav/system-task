namespace SRAAS.Api.Entities;

public class MessageReaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid OrgMemberId { get; set; }
    public string Emoji { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Message Message { get; set; } = null!;
    public OrgMember OrgMember { get; set; } = null!;
}
