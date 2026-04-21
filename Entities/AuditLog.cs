using System.Text.Json;

namespace SRAAS.Api.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = null!;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public JsonDocument? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public OrgMember? Actor { get; set; }
}
