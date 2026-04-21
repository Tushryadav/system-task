using System.Text.Json;

namespace SRAAS.Api.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int SeatLimit { get; set; } = 10;
    public int SeatsUsed { get; set; } = 0;
    public JsonDocument? Settings { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<App> Apps { get; set; } = new List<App>();
    public ICollection<OrgMember> OrgMembers { get; set; } = new List<OrgMember>();
    public ICollection<OrgInvite> OrgInvites { get; set; } = new List<OrgInvite>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Channel> Channels { get; set; } = new List<Channel>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
