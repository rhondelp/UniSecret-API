// File: Controllers/ConfessionsController.cs
using System.Security.Claims;
using System.Text.RegularExpressions;
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

    // GET: api/v1/confessions?universityId=1&categoryId=2&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConfessionDto>>> GetConfessions(
        [FromQuery] int? universityId,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Confessions
            .AsNoTracking()
            .Where(c => c.Status == ConfessionStatus.Approved);

        if (universityId.HasValue)
        {
            query = query.Where(c => c.UniversityId == universityId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

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
                c.IsAnonymous ? "Anonymous" : c.User.Name,
                c.IsAnonymous ? "anonymous" : c.User.Username,
                c.Status,
                c.ScheduledAt,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<ConfessionDto>(confessions, page, pageSize, totalCount, totalPages));
    }

    // GET: api/v1/confessions/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConfessionDto>> GetConfession(
        int id,
        CancellationToken cancellationToken = default)
    {
        var confession = await _context.Confessions
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                c.UniversityId,
                c.CategoryId,
                CategoryName = c.Category.Name,
                c.Body,
                c.IsAnonymous,
                AuthorName = c.IsAnonymous ? "Anonymous" : c.User.Name,
                AuthorUsername = c.IsAnonymous ? "anonymous" : c.User.Username,
                c.Status,
                c.ScheduledAt,
                c.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (confession is null)
        {
            return NotFound(new { message = $"Confession with ID {id} was not found." });
        }

        if (confession.Status != ConfessionStatus.Approved)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            var isAuthor = userIdClaim != null && int.TryParse(userIdClaim, out var userId) && userId == confession.UserId;
            var isAdmin = userRoleClaim is nameof(UserRole.Admin) or nameof(UserRole.SuperAdmin);

            if (!isAuthor && !isAdmin)
            {
                return NotFound(new { message = $"Confession with ID {id} was not found." });
            }
        }

        var dto = new ConfessionDto(
            confession.Id,
            confession.UniversityId,
            confession.CategoryId,
            confession.CategoryName,
            confession.Body,
            confession.IsAnonymous,
            confession.AuthorName,
            confession.AuthorUsername,
            confession.Status,
            confession.ScheduledAt,
            confession.CreatedAt
        );

        return Ok(dto);
    }

    // POST: api/v1/confessions
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ConfessionDto>> CreateConfession(
        CreateConfessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "User record not found." });
        }

        if (user.Status != UserStatus.Active)
        {
            return Forbid();
        }

        var universityExists = await _context.Universities
            .AsNoTracking()
            .AnyAsync(u => u.Id == dto.UniversityId, cancellationToken);

        if (!universityExists)
        {
            return BadRequest(new { message = "University not found." });
        }

        var categoryExists = await _context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == dto.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(new { message = "Category not found." });
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

        // --------------------------------------------------------
        // Extract and process Hashtags (#tag)
        // --------------------------------------------------------
        var matches = Regex.Matches(dto.Body, @"#([a-zA-Z0-9_]+)");
        var tags = matches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .Where(t => t.Length <= 50)
            .ToList();

        if (tags.Count > 0)
        {
            var existingHashtags = await _context.Hashtags
                .Where(h => tags.Contains(h.Tag))
                .ToListAsync(cancellationToken);

            var existingTagsMap = existingHashtags.ToDictionary(h => h.Tag, h => h);

            foreach (var tag in tags)
            {
                if (!existingTagsMap.TryGetValue(tag, out var hashtag))
                {
                    hashtag = new Hashtag { Tag = tag };
                    _context.Hashtags.Add(hashtag);
                    existingTagsMap[tag] = hashtag;
                }

                confession.ConfessionHashtags.Add(new ConfessionHashtag
                {
                    Confession = confession,
                    Hashtag = hashtag
                });
            }
        }

        _context.Confessions.Add(confession);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Confession submitted successfully and is pending review.",
            id = confession.Id
        });
    }
}