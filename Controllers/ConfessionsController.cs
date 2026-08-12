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

    // GET: api/v1/confessions?universityId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConfessionDto>>> GetConfessions([FromQuery] int? universityId)
    {
        var query = _context.Confessions
            .Include(c => c.Category)
            .Include(c => c.User)
            .Where(c => c.Status == ConfessionStatus.Approved) // Only show approved confessions publicly
            .AsQueryable();

        if (universityId.HasValue)
        {
            query = query.Where(c => c.UniversityId == universityId.Value);
        }

        var confessions = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConfessionDto(
                c.Id,
                c.UniversityId,
                c.CategoryId,
                c.Category.Name,
                c.Body,
                c.IsAnonymous,
                // Core Rule: Hide identity publicly if IsAnonymous is true
                c.IsAnonymous ? "Anonymous" : c.User.Name,
                c.IsAnonymous ? "anonymous" : c.User.Username,
                c.Status,
                c.ScheduledAt,
                c.CreatedAt
            ))
            .ToListAsync();

        return Ok(confessions);
    }

    // POST: api/v1/confessions
    [Authorize] // Requires JWT Token
    [HttpPost]
    public async Task<ActionResult<ConfessionDto>> CreateConfession(CreateConfessionDto dto)
    {
        // Extract User ID from JWT Claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        // Verify University and Category exist
        var university = await _context.Universities.FindAsync(dto.UniversityId);
        if (university is null) return BadRequest(new { message = "University not found." });

        var category = await _context.Categories.FindAsync(dto.CategoryId);
        if (category is null) return BadRequest(new { message = "Category not found." });

        var confession = new Confession
        {
            UserId = userId, // ALWAYS stored for internal accountability
            UniversityId = dto.UniversityId,
            CategoryId = dto.CategoryId,
            Body = dto.Body,
            IsAnonymous = dto.IsAnonymous,
            Status = ConfessionStatus.Pending, // Needs moderation approval by default
            ScheduledAt = dto.ScheduledAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Confessions.Add(confession);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Confession submitted successfully and is pending review." });
    }
}