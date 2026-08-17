using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// using UniSecretApi.Constants;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LikesController : ControllerBase
{
    private readonly AppDbContext _context;

    public LikesController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/v1/likes/toggle
    [Authorize]
    [HttpPost("toggle")]
    public async Task<ActionResult<LikeStatusDto>> ToggleLike(
        ToggleLikeDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var normalizedType = dto.LikeableType.Trim();
        if (normalizedType is not ("Confession" or "Comment"))
        {
            return BadRequest(new { message = "LikeableType must be 'Confession' or 'Comment'." });
        }

        // Verify target entity exists
        if (normalizedType == "Confession")
        {
            var exists = await _context.Confessions.AsNoTracking().AnyAsync(c => c.Id == dto.LikeableId, cancellationToken);
            if (!exists) return NotFound(new { message = "Confession not found." });
        }
        else
        {
            var exists = await _context.Comments.AsNoTracking().AnyAsync(c => c.Id == dto.LikeableId, cancellationToken);
            if (!exists) return NotFound(new { message = "Comment not found." });
        }

        var existingLike = await _context.Likes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.LikeableId == dto.LikeableId && l.LikeableType == normalizedType, cancellationToken);

        bool isLiked;
        if (existingLike is not null)
        {
            _context.Likes.Remove(existingLike);
            isLiked = false;
        }
        else
        {
            _context.Likes.Add(new Like
            {
                UserId = userId,
                LikeableId = dto.LikeableId,
                LikeableType = normalizedType,
                CreatedAt = DateTime.UtcNow
            });
            isLiked = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var totalLikes = await _context.Likes
            .AsNoTracking()
            .CountAsync(l => l.LikeableId == dto.LikeableId && l.LikeableType == normalizedType, cancellationToken);

        return Ok(new LikeStatusDto(isLiked, totalLikes));
    }

    // GET: api/v1/likes/status?likeableId=5&likeableType=Confession
    [HttpGet("status")]
    public async Task<ActionResult<LikeStatusDto>> GetLikeStatus(
        [FromQuery] int likeableId,
        [FromQuery] string likeableType,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = likeableType.Trim();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var userId);

        var isLiked = userId > 0 && await _context.Likes
            .AsNoTracking()
            .AnyAsync(l => l.UserId == userId && l.LikeableId == likeableId && l.LikeableType == normalizedType, cancellationToken);

        var totalLikes = await _context.Likes
            .AsNoTracking()
            .CountAsync(l => l.LikeableId == likeableId && l.LikeableType == normalizedType, cancellationToken);

        return Ok(new LikeStatusDto(isLiked, totalLikes));
    }
}