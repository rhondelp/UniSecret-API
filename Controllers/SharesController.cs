using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SharesController : ControllerBase
{
    private readonly AppDbContext _context;

    public SharesController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ShareDto>> ShareConfession(CreateShareDto dto, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var confession = await _context.Confessions
            .Include(c => c.Category)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == dto.ConfessionId, cancellationToken);

        if (confession == null)
            return NotFound(new { message = "Original confession not found." });

        var share = new Share
        {
            UserId = userId,
            ConfessionId = dto.ConfessionId,
            Caption = dto.Caption,
            CreatedAt = DateTime.UtcNow
        };

        _context.Shares.Add(share);
        await _context.SaveChangesAsync(cancellationToken);

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);

        var confessionDto = new ConfessionDto(
            confession.Id, 
            confession.UniversityId, 
            confession.CategoryId, 
            confession.Category.Name,
            confession.Body, 
            confession.IsAnonymous, 
            confession.IsAnonymous ? "Anonymous" : confession.User.Name,
            confession.IsAnonymous ? "anonymous" : confession.User.Username, 
            confession.Status,
            confession.ScheduledAt, 
            confession.CreatedAt, 
            0, 
            false, 
            false, 
            confession.ImageUrl
        );

        return Ok(new ShareDto(share.Id, userId, user!.Name, confession.Id, confessionDto, share.Caption, share.CreatedAt));
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<List<ShareDto>>> GetUserShares(int userId, CancellationToken cancellationToken)
    {
        var shares = await _context.Shares
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Confession)
                .ThenInclude(c => c.User)
            .Include(s => s.Confession)
                .ThenInclude(c => c.Category)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShareDto(
                s.Id,
                s.UserId,
                s.User.Name,
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
                    s.Confession.CreatedAt,
                    0,     
                    false, 
                    false,
                    s.Confession.ImageUrl // Fixed capital 'C'
                ),
                s.Caption,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Ok(shares);
    }
}