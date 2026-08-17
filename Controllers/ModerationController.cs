using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;
using UniSecretApi.Enums;

namespace UniSecretApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/v1/[controller]")]
public class ModerationController : ControllerBase
{
    private readonly AppDbContext _context;

    public ModerationController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/moderation/queue?page=1&pageSize=20
    [HttpGet("queue")]
    public async Task<ActionResult<PagedResult<ConfessionDto>>> GetPendingQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Confessions
            .AsNoTracking()
            .Where(c => c.Status == ConfessionStatus.Pending);

        var totalCount = await query.CountAsync(cancellationToken);

        var pendingConfessions = await query
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConfessionDto(
                c.Id,
                c.UniversityId,
                c.CategoryId,
                c.Category.Name,
                c.Body,
                c.IsAnonymous,
                c.IsAnonymous ? "Anonymous" : c.User.Name,
                c.IsAnonymous ? "anonymous" : c.User.Username,
                c.Status,
                c.ScheduledAt,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<ConfessionDto>(pendingConfessions, page, pageSize, totalCount, totalPages));
    }

    // POST: api/v1/moderation/confessions/5/review
    [HttpPost("confessions/{id:int}/review")]
    public async Task<IActionResult> ReviewConfession(
        int id,
        ReviewConfessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var moderatorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (moderatorIdClaim is null || !int.TryParse(moderatorIdClaim, out var moderatorId))
        {
            return Unauthorized();
        }

        var confession = await _context.Confessions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (confession is null)
        {
            return NotFound(new { message = $"Confession with ID {id} was not found." });
        }

        var newStatus = dto.Approve ? ConfessionStatus.Approved : ConfessionStatus.Rejected;
        confession.Status = newStatus;
        confession.UpdatedAt = DateTime.UtcNow;

        var log = new ModerationLog
        {
            AdminId = moderatorId,
            Action = dto.Approve ? "ApproveConfession" : "RejectConfession",
            TargetType = "Confession",
            TargetId = confession.Id,
            Notes = dto.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.ModerationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = $"Confession has been {(dto.Approve ? "approved" : "rejected")}.",
            confessionId = confession.Id,
            status = confession.Status
        });
    }

    // POST: api/v1/moderation/users/5/status
    [HttpPost("users/{id:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        int id,
        UpdateUserStatusDto dto,
        CancellationToken cancellationToken = default)
    {
        var moderatorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (moderatorIdClaim is null || !int.TryParse(moderatorIdClaim, out var moderatorId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = $"User with ID {id} was not found." });
        }

        user.Status = dto.Status;
        user.UpdatedAt = DateTime.UtcNow;

        var log = new ModerationLog
        {
            AdminId = moderatorId,
            Action = $"UpdateUserStatusTo_{dto.Status}",
            TargetType = "User",
            TargetId = user.Id,
            Notes = dto.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.ModerationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"User status updated to {dto.Status}.", userId = user.Id });
    }

    // GET: api/v1/moderation/logs?page=1&pageSize=20
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<ModerationLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.ModerationLogs.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ModerationLogDto(
                l.Id,
                l.AdminId,
                l.Admin.Username,
                l.Action,
                l.TargetType,
                l.TargetId,
                l.Notes,
                l.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<ModerationLogDto>(logs, page, pageSize, totalCount, totalPages));
    }
}