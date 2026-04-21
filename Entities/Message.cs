using SRAAS.Api.Enums;
using System.Text.Json;

namespace SRAAS.Api.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid OrgId { get; set; }
    public Guid? SenderId { get; set; }
    public Guid? ReplyToId { get; set; }
    public string? Content { get; set; }
    public ContentTypeEnum ContentType { get; set; } = ContentTypeEnum.Text;
    public JsonDocument? Metadata { get; set; }
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Channel Channel { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public OrgMember? Sender { get; set; }
    public Message? ReplyTo { get; set; }
    public ICollection<Message> Replies { get; set; } = new List<Message>();
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
