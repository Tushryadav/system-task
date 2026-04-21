using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Enums;
using SRAAS.Api.Services;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IAuditService _audit;

    public MembersController(SraasDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// GET /api/members — Bearer. List all members in the org.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListMembers()
    {
        var orgId = GetCurrentOrgId();

        var members = await _db.OrgMembers
            .Where(m => m.OrgId == orgId && m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new MemberResponse(
                m.Id, m.Name, m.Email,
                m.Role.ToString().ToLower(),
                m.Status.ToString().ToLower(),
                m.IsActive, m.JoinedAt))
            .ToListAsync();

        return Ok(members);
    }

    /// <summary>
    /// DELETE /api/members/{memberId} — Admin only. Soft-delete a member and free a seat.
    /// </summary>
    [HttpDelete("{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid memberId)
    {
        var (callerId, orgId, role) = GetCurrentUser();

        if (role != "admin")
            return Forbid();

        var member = await _db.OrgMembers
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrgId == orgId);

        if (member == null) return NotFound();

        if (member.Id == callerId)
            return BadRequest(new { message = "You cannot remove yourself." });

        using var tx = await _db.Database.BeginTransactionAsync();

        // Soft delete
        member.IsActive = false;
        member.Status = MemberStatusEnum.Suspended;
        member.DeletedAt = DateTime.UtcNow;

        // Free the seat
        member.Organization.SeatsUsed = Math.Max(0, member.Organization.SeatsUsed - 1);

        // Revoke all sessions
        await _db.RefreshTokens
            .Where(t => t.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));

        await _db.SaveChangesAsync();

        // Audit log
        await _audit.LogAsync(orgId, callerId, "member.removed",
            targetType: "member", targetId: memberId);

        await tx.CommitAsync();

        return Ok(new { message = "Member removed." });
    }

    private Guid GetCurrentOrgId()
    {
        return Guid.Parse(User.FindFirst("org_id")?.Value!);
    }

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
