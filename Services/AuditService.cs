using SRAAS.Api.Data;
using SRAAS.Api.Entities;
using System.Text.Json;

namespace SRAAS.Api.Services;

public interface IAuditService
{
    Task LogAsync(
        Guid orgId,
        Guid? actorId,
        string action,
        string? targetType = null,
        Guid? targetId = null,
        object? metadata = null);
}

public class AuditService : IAuditService
{
    private readonly SraasDbContext _db;

    public AuditService(SraasDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        Guid orgId,
        Guid? actorId,
        string action,
        string? targetType = null,
        Guid? targetId = null,
        object? metadata = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            OrgId = orgId,
            ActorId = actorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Metadata = metadata != null
                ? JsonDocument.Parse(JsonSerializer.Serialize(metadata))
                : null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
