namespace SRAAS.Api.Entities;

public class ChannelMember
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid OrgMemberId { get; set; }
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Channel Channel { get; set; } = null!;
    public OrgMember OrgMember { get; set; } = null!;
}
