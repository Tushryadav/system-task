using SRAAS.Api.Enums;

namespace SRAAS.Api.Entities;

public class OrgInvite
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid? CreatedBy { get; set; }
    public string InviteCode { get; set; } = null!;
    public InviteTypeEnum InviteType { get; set; } = InviteTypeEnum.Multi;
    public int MaxUses { get; set; } = 1;
    public int UsedCount { get; set; } = 0;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public OrgMember? Creator { get; set; }
    public ICollection<OrgMember> JoinedMembers { get; set; } = new List<OrgMember>();
}
