using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Entities;
using SRAAS.Api.Enums;
using SRAAS.Api.Services;
using System.Text.Json;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPasswordService _passwordService;

    public OrganizationsController(SraasDbContext db, IAuditService audit, IPasswordService passwordService)
    {
        _db = db;
        _audit = audit;
        _passwordService = passwordService;
    }

    /// <summary>
    /// POST /api/orgs/register — Public. Register a new organization and admin account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterOrg([FromBody] RegisterOrgRequest request)
    {
        // Check if slug is taken
        if (await _db.Organizations.AnyAsync(o => o.Slug == request.OrgSlug))
            return BadRequest(new { message = "Organization slug is already taken." });

        // Check if email is globally taken (or in any org)
        if (await _db.OrgMembers.AnyAsync(m => m.Email == request.AdminEmail))
            return BadRequest(new { message = "Email is already registered." });

        using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            var org = new Organization
            {
                Name = request.OrgName,
                Slug = request.OrgSlug,
                SeatLimit = 10,
                SeatsUsed = 1
            };

            _db.Organizations.Add(org);
            await _db.SaveChangesAsync(); // save to generate org.Id

            var admin = new OrgMember
            {
                OrgId = org.Id,
                Name = request.AdminName,
                Email = request.AdminEmail,
                PasswordHash = _passwordService.Hash(request.AdminPassword),
                Role = MemberRoleEnum.Admin,
                Status = MemberStatusEnum.Active,
                IsActive = true
            };

            _db.OrgMembers.Add(admin);
            await _db.SaveChangesAsync(); // save to generate admin.Id

            _db.AuditLogs.Add(new AuditLog
            {
                OrgId = org.Id,
                ActorId = admin.Id,
                Action = "org.created",
                TargetType = "organization",
                TargetId = org.Id
            });

            _db.AuditLogs.Add(new AuditLog
            {
                OrgId = org.Id,
                ActorId = admin.Id,
                Action = "member.joined",
                TargetType = "member",
                TargetId = admin.Id,
                Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { role = "admin", method = "registration" }))
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                message = "Organization registered successfully.",
                orgId = org.Id,
                adminId = admin.Id
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
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
