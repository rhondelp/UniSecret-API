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
[Route("api/v1/[controller]")]
public class ConfessionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConfessionsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/confessions?universityId=1&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConfessionDto>>> GetConfessions(
        [FromQuery] int? universityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Prevent clients from requesting extremely large result sets.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Confessions
            .AsNoTracking()
            .Where(c => c.Status == ConfessionStatus.Approved);

        if (universityId.HasValue)
        {
            query = query.Where(
                c => c.UniversityId == universityId.Value);
        }

        var confessions = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConfessionDto(
                c.Id,
                c.UniversityId,
                c.CategoryId,
                c.Category.Name,
                c.Body,
                c.IsAnonymous,
                c.IsAnonymous
                    ? "Anonymous"
                    : c.User.Name,
                c.IsAnonymous
                    ? "anonymous"
                    : c.User.Username,
                c.Status,
                c.ScheduledAt,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(confessions);
    }

    // POST: api/v1/confessions
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ConfessionDto>> CreateConfession(
        CreateConfessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim is null ||
            !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(
                new { message = "Invalid token claims." });
        }

        // --------------------------------------------------------
        // Verify University
        // --------------------------------------------------------

        var universityExists =
            await _context.Universities
                .AsNoTracking()
                .AnyAsync(
                    u => u.Id == dto.UniversityId,
                    cancellationToken);

        if (!universityExists)
        {
            return BadRequest(
                new { message = "University not found." });
        }

        // --------------------------------------------------------
        // Verify Category
        // --------------------------------------------------------

        var categoryExists =
            await _context.Categories
                .AsNoTracking()
                .AnyAsync(
                    c => c.Id == dto.CategoryId,
                    cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(
                new { message = "Category not found." });
        }

        var now = DateTime.UtcNow;

        var confession = new Confession
        {
            UserId = userId,
            UniversityId = dto.UniversityId,
            CategoryId = dto.CategoryId,
            Body = dto.Body,
            IsAnonymous = dto.IsAnonymous,
            Status = ConfessionStatus.Pending,
            ScheduledAt = dto.ScheduledAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Confessions.Add(confession);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Confession submitted successfully and is pending review."
        });
    }
}