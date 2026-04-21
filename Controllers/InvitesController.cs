using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Entities;
using SRAAS.Api.Enums;
using SRAAS.Api.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/invites")]
public class InvitesController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _audit;

    public InvitesController(SraasDbContext db, IPasswordService passwordService, IAuditService audit)
    {
        _db = db;
        _passwordService = passwordService;
        _audit = audit;
    }

    /// <summary>
    /// POST /api/invites/create — Admin only. Generate a new invite code.
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteRequest request)
    {
        var (memberId, orgId, role) = GetCurrentUser();
        if (role != "admin")
            return Forbid();

        var org = await _db.Organizations.FindAsync(orgId);
        if (org == null) return NotFound();

        var code = GenerateInviteCode();
        var inviteType = request.InviteType?.ToLower() == "single"
            ? InviteTypeEnum.Single
            : InviteTypeEnum.Multi;

        var invite = new OrgInvite
        {
            OrgId = orgId,
            CreatedBy = memberId,
            InviteCode = code,
            InviteType = inviteType,
            MaxUses = inviteType == InviteTypeEnum.Single ? 1 : request.MaxUses,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiryDays)
        };

        _db.OrgInvites.Add(invite);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(orgId, memberId, "invite.created",
            targetType: "invite", targetId: invite.Id,
            metadata: new { inviteCode = code, maxUses = invite.MaxUses });

        var inviteUrl = $"https://app.sraas.com/join/{org.Slug}/{code}";

        return Ok(new InviteResponse(
            invite.Id, code, invite.InviteType.ToString().ToLower(),
            invite.MaxUses, invite.UsedCount, invite.ExpiresAt,
            invite.IsActive, inviteUrl, invite.CreatedAt));
    }

    /// <summary>
    /// GET /api/invites — Admin only. List all invites for the org.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListInvites()
    {
        var (_, orgId, role) = GetCurrentUser();
        if (role != "admin")
            return Forbid();

        var org = await _db.Organizations.FindAsync(orgId);
        if (org == null) return NotFound();

        var invites = await _db.OrgInvites
            .Where(i => i.OrgId == orgId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteResponse(
                i.Id, i.InviteCode, i.InviteType.ToString().ToLower(),
                i.MaxUses, i.UsedCount, i.ExpiresAt,
                i.IsActive,
                $"https://app.sraas.com/join/{org.Slug}/{i.InviteCode}",
                i.CreatedAt))
            .ToListAsync();

        return Ok(invites);
    }

    /// <summary>
    /// DELETE /api/invites/{id} — Admin only. Deactivate an invite.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeactivateInvite(Guid id)
    {
        var (memberId, orgId, role) = GetCurrentUser();
        if (role != "admin")
            return Forbid();

        var invite = await _db.OrgInvites
            .FirstOrDefaultAsync(i => i.Id == id && i.OrgId == orgId);

        if (invite == null) return NotFound();

        invite.IsActive = false;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(orgId, memberId, "invite.deactivated",
            targetType: "invite", targetId: invite.Id);

        return Ok(new { message = "Invite deactivated." });
    }

    /// <summary>
    /// POST /api/invites/join — Public. Join an org via invite code.
    /// Transaction-safe with race condition prevention.
    /// </summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinOrg([FromBody] JoinRequest request)
    {
        // Validate invite
        var invite = await _db.OrgInvites
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i =>
                i.InviteCode == request.InviteCode &&
                i.Organization.Slug == request.OrgSlug &&
                i.IsActive &&
                i.ExpiresAt > DateTime.UtcNow);

        if (invite == null)
            return BadRequest(new { message = "Invalid or expired invite link." });

        if (invite.UsedCount >= invite.MaxUses)
            return BadRequest(new { message = "This invite link has reached its limit." });

        // Begin transaction — prevent race conditions on seat count
        using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // Actual DB count — do not trust cached seats_used alone
            var actualCount = await _db.OrgMembers
                .CountAsync(m => m.OrgId == invite.OrgId && m.IsActive);

            if (actualCount >= invite.Organization.SeatLimit)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "Organisation is full. Contact your admin." });
            }

            // Check duplicate email within the org
            var exists = await _db.OrgMembers
                .AnyAsync(m => m.OrgId == invite.OrgId && m.Email == request.Email);

            if (exists)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "An account with this email already exists in this organisation." });
            }

            // Create member
            var member = new OrgMember
            {
                OrgId = invite.OrgId,
                InviteId = invite.Id,
                Name = request.Name,
                Email = request.Email,
                PasswordHash = _passwordService.Hash(request.Password),
                Role = MemberRoleEnum.Member
            };

            _db.OrgMembers.Add(member);
            invite.UsedCount++;
            invite.Organization.SeatsUsed++;

            await _db.SaveChangesAsync();

            // Audit log
            _db.AuditLogs.Add(new AuditLog
            {
                OrgId = invite.OrgId,
                ActorId = member.Id,
                Action = "member.joined",
                TargetType = "member",
                TargetId = member.Id,
                Metadata = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { inviteCode = invite.InviteCode }))
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { message = "Joined successfully.", memberId = member.Id });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No O, 0, I, 1
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
