using SRAAS.Api.Enums;

namespace SRAAS.Api.Entities;

public class OrgMember
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid? InviteId { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public MemberRoleEnum Role { get; set; } = MemberRoleEnum.Member;
    public MemberStatusEnum Status { get; set; } = MemberStatusEnum.Active;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public OrgInvite? Invite { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<ChannelMember> ChannelMemberships { get; set; } = new List<ChannelMember>();
    public ICollection<AppMember> AppMemberships { get; set; } = new List<AppMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
