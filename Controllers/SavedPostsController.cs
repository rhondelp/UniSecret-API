// File: Controllers/SavedPostsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;

namespace UniSecretApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class SavedPostsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SavedPostsController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/v1/saved-posts/5/toggle
    [HttpPost("{confessionId:int}/toggle")]
    public async Task<IActionResult> ToggleSavePost(
        int confessionId,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var confessionExists = await _context.Confessions
            .AsNoTracking()
            .AnyAsync(c => c.Id == confessionId, cancellationToken);

        if (!confessionExists)
        {
            return NotFound(new { message = $"Confession with ID {confessionId} was not found." });
        }

        var savedPost = await _context.SavedPosts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ConfessionId == confessionId, cancellationToken);

        bool isSaved;
        if (savedPost is not null)
        {
            _context.SavedPosts.Remove(savedPost);
            isSaved = false;
        }
        else
        {
            _context.SavedPosts.Add(new SavedPost
            {
                UserId = userId,
                ConfessionId = confessionId,
                CreatedAt = DateTime.UtcNow
            });
            isSaved = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { isSaved, message = isSaved ? "Confession saved." : "Confession removed from saved." });
    }

    // GET: api/v1/saved-posts?page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<PagedResult<SavedPostDto>>> GetSavedPosts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.SavedPosts
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var savedPosts = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SavedPostDto(
                s.Id,
                s.ConfessionId,
                new ConfessionDto(
                    s.Confession.Id,
                    s.Confession.UniversityId,
                    s.Confession.CategoryId,
                    s.Confession.Category.Name,
                    s.Confession.Body,
                    s.Confession.IsAnonymous,
                    s.Confession.IsAnonymous ? "Anonymous" : s.Confession.User.Name,
                    s.Confession.IsAnonymous ? "anonymous" : s.Confession.User.Username,
                    s.Confession.Status,
                    s.Confession.ScheduledAt,
                    s.Confession.CreatedAt
                ),
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<SavedPostDto>(savedPosts, page, pageSize, totalCount, totalPages));
    }
}