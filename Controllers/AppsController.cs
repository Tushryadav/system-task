using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Data;
using SRAAS.Api.DTOs;

namespace SRAAS.Api.Controllers;

[ApiController]
[Route("api/apps")]
[Authorize]
public class AppsController : ControllerBase
{
    private readonly SraasDbContext _db;

    public AppsController(SraasDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/apps — Bearer. List all apps in the current org.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListApps()
    {
        var orgId = GetCurrentOrgId();

        var apps = await _db.Apps
            .Where(a => a.OrgId == orgId && a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new AppResponse(
                a.Id, a.Name,
                a.AppType.ToString().ToLower(),
                a.IsActive, a.CreatedAt))
            .ToListAsync();

        return Ok(apps);
    }

    /// <summary>
    /// GET /api/apps/{id}/channels — Bearer. List channels belonging to a specific app.
    /// </summary>
    [HttpGet("{id:guid}/channels")]
    public async Task<IActionResult> ListChannels(Guid id)
    {
        var orgId = GetCurrentOrgId();

        var channels = await _db.Channels
            .Where(c => c.AppId == id && c.OrgId == orgId)
            .OrderBy(c => c.Name)
            .Select(c => new ChannelResponse(
                c.Id, c.Name,
                c.ChannelType.ToString().ToLower(),
                c.IsPrivate, c.CreatedAt))
            .ToListAsync();

        return Ok(channels);
    }

    private Guid GetCurrentOrgId()
    {
        return Guid.Parse(User.FindFirst("org_id")?.Value!);
    }
}
