using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;
using SRAAS.Api.Entities;
using SRAAS.Api.Services;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly SraasDbContext _db;
    private readonly IAuditService _audit;

    // Allowed MIME types — validated server-side via magic bytes
    private static readonly HashSet<string> AllowedMimeTypes = new()
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private const int MaxFileSizeKb = 10 * 1024; // 10 MB

    public FilesController(SraasDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// POST /api/files/upload?messageId={messageId} — Bearer. Upload an attachment.
    /// Server-side MIME type validation using magic bytes.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] Guid messageId)
    {
        var (memberId, orgId, _) = GetCurrentUser();

        if (file.Length > MaxFileSizeKb * 1024)
            return BadRequest(new { message = "File too large. Maximum is 10 MB." });

        // Read magic bytes for real MIME type detection
        using var stream = file.OpenReadStream();
        var buffer = new byte[8];
        await stream.ReadExactlyAsync(buffer, 0, Math.Min(8, (int)file.Length));
        var detectedMime = DetectMimeFromBytes(buffer);

        if (!AllowedMimeTypes.Contains(detectedMime))
            return BadRequest(new { message = $"File type '{detectedMime}' not allowed." });

        // In production: upload to S3 here and get the storage key
        // For now, generate a placeholder storage key
        var storageKey = $"{orgId}/attachments/{Guid.NewGuid()}";

        var attachment = new MessageAttachment
        {
            MessageId = messageId,
            OrgId = orgId,
            FileName = file.FileName,
            FileType = detectedMime,
            FileSizeKb = (int)(file.Length / 1024),
            StorageKey = storageKey
        };

        _db.MessageAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(orgId, memberId, "file.uploaded",
            "attachment", attachment.Id,
            new { fileName = file.FileName, fileType = detectedMime, sizeKb = attachment.FileSizeKb });

        return Ok(new AttachmentResponse(attachment.Id, attachment.FileName, attachment.FileType, attachment.FileSizeKb));
    }

    /// <summary>
    /// GET /api/files/{id}/url — Bearer. Get a signed download URL for an attachment.
    /// In production this generates a 15-minute S3 pre-signed URL.
    /// </summary>
    [HttpGet("{id:guid}/url")]
    public async Task<IActionResult> GetSignedUrl(Guid id)
    {
        var orgId = GetCurrentOrgId();

        var attachment = await _db.MessageAttachments
            .FirstOrDefaultAsync(a => a.Id == id && a.OrgId == orgId);

        if (attachment == null) return NotFound();

        // In production: generate a real S3 pre-signed URL here
        // var signedUrl = await _s3Service.GetSignedUrlAsync(attachment.StorageKey, 15);
        var signedUrl = $"https://s3.amazonaws.com/sraas-bucket/{attachment.StorageKey}?signed=placeholder&expires=15m";

        return Ok(new { url = signedUrl, expiresInMinutes = 15 });
    }

    /// <summary>
    /// Detect MIME type from file magic bytes.
    /// </summary>
    private static string DetectMimeFromBytes(byte[] bytes)
    {
        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        // GIF: 47 49 46
        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";

        // PDF: 25 50 44 46
        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
            return "application/pdf";

        // WEBP: 52 49 46 46 ... 57 45 42 50
        if (bytes.Length >= 4 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
            return "image/webp";

        // DOCX (ZIP-based): 50 4B 03 04
        if (bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return "application/octet-stream";
    }

    private Guid GetCurrentOrgId() => Guid.Parse(User.FindFirst("org_id")?.Value!);

    private (Guid memberId, Guid orgId, string role) GetCurrentUser()
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value!);
        var orgId = Guid.Parse(User.FindFirst("org_id")?.Value!);
        var role = User.FindFirst("role")?.Value ?? "member";
        return (memberId, orgId, role);
    }
}
