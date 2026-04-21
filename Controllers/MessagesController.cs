using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Entities;
using SRAAS.Api.Enums;
using SRAAS.Api.Services;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IAuditService _audit;

    public MessagesController(SraasDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// POST /api/messages?channelId={channelId} — Bearer. Send a message to a channel.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromQuery] Guid channelId, [FromBody] SendMessageRequest request)
    {
        var (memberId, orgId, _) = GetCurrentUser();

        var channel = await _db.Channels.FirstOrDefaultAsync(c => c.Id == channelId && c.OrgId == orgId);
        if (channel == null)
            return NotFound(new { message = "Channel not found." });

        var contentType = Enum.TryParse<ContentTypeEnum>(request.ContentType, true, out var ct)
            ? ct : ContentTypeEnum.Text;

        var message = new Message
        {
            ChannelId = channelId,
            OrgId = orgId,
            SenderId = memberId,
            Content = request.Content,
            ContentType = contentType,
            ReplyToId = request.ReplyToId
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse(
            message.Id, message.ChannelId, message.SenderId, null,
            message.ReplyToId, message.Content,
            message.ContentType.ToString().ToLower(),
            message.IsEdited, message.IsDeleted,
            message.CreatedAt, message.UpdatedAt, null, null));
    }

    /// <summary>
    /// DELETE /api/messages/{id} — Bearer. Soft-delete a message (sender or admin).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var (memberId, orgId, role) = GetCurrentUser();

        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id && m.OrgId == orgId);
        if (message == null) return NotFound();

        // Only sender or admin can delete
        if (message.SenderId != memberId && role != "admin")
            return Forbid();

        message.IsDeleted = true;
        message.Content = null;
        message.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(orgId, memberId, "message.deleted", "message", id);

        return Ok(new { message = "Message deleted." });
    }

    /// <summary>
    /// POST /api/messages/{id}/reactions — Bearer. Add a reaction to a message.
    /// </summary>
    [HttpPost("{id:guid}/reactions")]
    public async Task<IActionResult> AddReaction(Guid id, [FromBody] AddReactionRequest request)
    {
        var (memberId, orgId, _) = GetCurrentUser();

        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id && m.OrgId == orgId);
        if (message == null) return NotFound();

        // Check if already reacted with same emoji
        var exists = await _db.MessageReactions.AnyAsync(r =>
            r.MessageId == id && r.OrgMemberId == memberId && r.Emoji == request.Emoji);

        if (exists)
            return BadRequest(new { message = "You already reacted with this emoji." });

        _db.MessageReactions.Add(new MessageReaction
        {
            MessageId = id,
            OrgMemberId = memberId,
            Emoji = request.Emoji
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Reaction added." });
    }

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
