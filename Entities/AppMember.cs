namespace SRAAS.Api.Entities;

public class AppMember
{
    public Guid Id { get; set; }
    public Guid AppId { get; set; }
    public Guid OrgMemberId { get; set; }
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public App App { get; set; } = null!;
    public OrgMember OrgMember { get; set; } = null!;
}
