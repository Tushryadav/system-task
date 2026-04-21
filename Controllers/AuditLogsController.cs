using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly SraasDbContext _db;

    public AuditLogsController(SraasDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/audit-logs — Admin only. View audit log (paginated).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (_, orgId, role) = GetCurrentUser();
        if (role != "admin")
            return Forbid();

        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var logs = await _db.AuditLogs
            .Where(l => l.OrgId == orgId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(l => l.Actor)
            .Select(l => new AuditLogResponse(
                l.Id, l.ActorId, l.Actor != null ? l.Actor.Name : null,
                l.Action, l.TargetType, l.TargetId, l.CreatedAt))
            .ToListAsync();

        var totalCount = await _db.AuditLogs.CountAsync(l => l.OrgId == orgId);

        return Ok(new
        {
            data = logs,
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
