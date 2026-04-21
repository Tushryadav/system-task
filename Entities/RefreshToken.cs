using System.Text.Json;

namespace SRAAS.Api.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid OrgId { get; set; }
    public string TokenHash { get; set; } = null!;
    public JsonDocument? DeviceInfo { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public OrgMember Member { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
