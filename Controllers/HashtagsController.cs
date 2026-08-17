using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HashtagsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HashtagsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/hashtags/trending?limit=10

    [HttpGet("trending")]
    public async Task<ActionResult<IEnumerable<HashtagDto>>> GetTrendingHashtags(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var trending = await _context.Hashtags
            .AsNoTracking()
            .Where(h => h.ConfessionHashtags.Count > 0)
            .OrderByDescending(h => h.ConfessionHashtags.Count)
            .ThenBy(h => h.Tag)
            .Take(limit)
            .Select(h => new HashtagDto(
                h.Id,
                h.Tag,
                h.ConfessionHashtags.Count
            ))
            .ToListAsync(cancellationToken);

        return Ok(trending);
    }

    // GET: api/v1/hashtags/search?q=exam
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<HashtagDto>>> SearchHashtags(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<HashtagDto>());
        }

        var searchTerm = q.Trim().ToLowerInvariant().TrimStart('#');
        limit = Math.Clamp(limit, 1, 30);

        var results = await _context.Hashtags
            .AsNoTracking()
            .Where(h => h.Tag.Contains(searchTerm))
            .Select(h => new HashtagDto(
                h.Id,
                h.Tag,
                h.ConfessionHashtags.Count
            ))
            .OrderByDescending(h => h.UsageCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(results);
    }
}