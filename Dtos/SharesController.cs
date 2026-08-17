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
            confession.Id, confession.UniversityId, confession.CategoryId, confession.Category.Name,
            confession.Body, confession.IsAnonymous, confession.IsAnonymous ? "Anonymous" : confession.User.Name,
            confession.IsAnonymous ? "anonymous" : confession.User.Username, confession.Status,
            confession.ScheduledAt, confession.CreatedAt
        );

        return Ok(new ShareDto(share.Id, userId, user!.Name, confession.Id, confessionDto, share.Caption, share.CreatedAt));
    }
}