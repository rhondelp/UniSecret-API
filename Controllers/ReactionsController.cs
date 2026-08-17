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
public class ReactionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReactionsController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize]
    [HttpPost("set")]
    public async Task<ActionResult<ReactionStatusDto>> SetReaction(
        SetReactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var normalizedType = dto.ReactableType.Trim();
        var existing = await _context.Reactions
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ReactableId == dto.ReactableId && r.ReactableType == normalizedType, cancellationToken);

        if (existing != null)
        {
            if (existing.Type == dto.Type)
            {
                _context.Reactions.Remove(existing); // Toggle off if same reaction tapped
            }
            else
            {
                existing.Type = dto.Type; // Change reaction type
            }
        }
        else
        {
            _context.Reactions.Add(new Reaction
            {
                UserId = userId,
                ReactableId = dto.ReactableId,
                ReactableType = normalizedType,
                Type = dto.Type,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetReactionSummary(dto.ReactableId, normalizedType, userId, cancellationToken);
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<ReactionUserDto>>> GetReactors(
        [FromQuery] int reactableId,
        [FromQuery] string reactableType,
        [FromQuery] ReactionType? filterType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Reactions
            .AsNoTracking()
            .Where(r => r.ReactableId == reactableId && r.ReactableType == reactableType);

        if (filterType.HasValue)
        {
            query = query.Where(r => r.Type == filterType.Value);
        }

        var reactors = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReactionUserDto(
                r.UserId,
                r.User.Name,
                r.User.Username,
                r.Type,
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(reactors);
    }

    private async Task<ActionResult<ReactionStatusDto>> GetReactionSummary(int reactableId, string reactableType, int userId, CancellationToken cancellationToken)
    {
        var reactions = await _context.Reactions
            .AsNoTracking()
            .Where(r => r.ReactableId == reactableId && r.ReactableType == reactableType)
            .ToListAsync(cancellationToken);

        var userReaction = reactions.FirstOrDefault(r => r.UserId == userId)?.Type;

        var counts = reactions
            .GroupBy(r => r.Type)
            .Select(g => new ReactionCountSummaryDto(g.Key, g.Count()))
            .ToList();

        return Ok(new ReactionStatusDto(userReaction, reactions.Count, counts));
    }
}