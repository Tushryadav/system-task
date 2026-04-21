using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Services;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IAuditService _audit;

    public OrganizationsController(SraasDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// GET /api/orgs/me — Bearer. Get current user's organization info.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyOrg()
    {
        var orgId = GetCurrentOrgId();
        var org = await _db.Organizations.FindAsync(orgId);

        if (org == null)
            return NotFound();

        return Ok(new OrgResponse(
            org.Id, org.Name, org.Slug,
            org.SeatLimit, org.SeatsUsed, org.CreatedAt));
    }

    /// <summary>
    /// PUT /api/orgs/seat-limit — Admin only. Update the organization's seat limit.
    /// </summary>
    [HttpPut("seat-limit")]
    public async Task<IActionResult> UpdateSeatLimit([FromBody] UpdateSeatLimitRequest request)
    {
        var (memberId, orgId, role) = GetCurrentUser();

        if (role != "admin")
            return Forbid();

        var org = await _db.Organizations.FindAsync(orgId);
        if (org == null) return NotFound();

        var oldLimit = org.SeatLimit;
        org.SeatLimit = request.NewSeatLimit;
        org.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(orgId, memberId, "seat_limit.changed",
            targetType: "organization", targetId: orgId,
            metadata: new { oldLimit, newLimit = request.NewSeatLimit });

        return Ok(new { message = "Seat limit updated.", newSeatLimit = request.NewSeatLimit });
    }

    private Guid GetCurrentOrgId()
    {
        var orgId = User.FindFirst("org_id")?.Value;
        return Guid.Parse(orgId!);
    }

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
