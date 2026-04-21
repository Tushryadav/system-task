using SRAAS.Api.Enums;
using System.Text.Json;

namespace SRAAS.Api.Entities;

public class App
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string Name { get; set; } = null!;
    public AppTypeEnum AppType { get; set; } = AppTypeEnum.Chat;
    public JsonDocument? Config { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public ICollection<Channel> Channels { get; set; } = new List<Channel>();
    public ICollection<AppMember> AppMembers { get; set; } = new List<AppMember>();
}
