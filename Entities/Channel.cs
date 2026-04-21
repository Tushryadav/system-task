using SRAAS.Api.Enums;

namespace SRAAS.Api.Entities;

public class Channel
{
    public Guid Id { get; set; }
    public Guid AppId { get; set; }
    public Guid OrgId { get; set; }
    public string? Name { get; set; }
    public ChannelTypeEnum ChannelType { get; set; } = ChannelTypeEnum.General;
    public bool IsPrivate { get; set; } = false;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public App App { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public OrgMember? Creator { get; set; }
    public ICollection<ChannelMember> ChannelMembers { get; set; } = new List<ChannelMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
