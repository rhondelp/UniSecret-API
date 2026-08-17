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
[Route("api/v1")]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CommentsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/confessions/5/comments?page=1&pageSize=20
    [HttpGet("confessions/{confessionId:int}/comments")]
    public async Task<ActionResult<PagedResult<CommentDto>>> GetComments(
        int confessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var confessionExists = await _context.Confessions
            .AsNoTracking()
            .AnyAsync(c => c.Id == confessionId && c.Status == ConfessionStatus.Approved, cancellationToken);

        if (!confessionExists)
        {
            return NotFound(new { message = $"Approved confession with ID {confessionId} was not found." });
        }

        // Query top-level comments (ParentId == null)
        var query = _context.Comments
            .AsNoTracking()
            .Where(c => c.ConfessionId == confessionId && c.ParentId == null);

        var totalCount = await query.CountAsync(cancellationToken);

        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentDto(
                c.Id,
                c.ConfessionId,
                c.ParentId,
                c.Body,
                c.IsAnonymous,
                c.IsAnonymous ? "Anonymous" : c.User.Name,
                c.IsAnonymous ? "anonymous" : c.User.Username,
                _context.Likes.Count(l => l.LikeableId == c.Id && l.LikeableType == "Comment"),
                c.CreatedAt,
                c.Replies
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new CommentDto(
                        r.Id,
                        r.ConfessionId,
                        r.ParentId,
                        r.Body,
                        r.IsAnonymous,
                        r.IsAnonymous ? "Anonymous" : r.User.Name,
                        r.IsAnonymous ? "anonymous" : r.User.Username,
                        _context.Likes.Count(l => l.LikeableId == r.Id && l.LikeableType == "Comment"),
                        r.CreatedAt,
                        Array.Empty<CommentDto>()
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<CommentDto>(comments, page, pageSize, totalCount, totalPages));
    }

    // POST: api/v1/confessions/5/comments
    [Authorize]
    [HttpPost("confessions/{confessionId:int}/comments")]
    public async Task<ActionResult<CommentDto>> CreateComment(
        int confessionId,
        CreateCommentDto dto,
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
            .Select(u => new { u.Status, u.Name, u.Username })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return Forbid();
        }

        var confession = await _context.Confessions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == confessionId && c.Status == ConfessionStatus.Approved, cancellationToken);

        if (confession is null)
        {
            return NotFound(new { message = "Confession not found or not approved." });
        }

        if (dto.ParentId.HasValue)
        {
            var parentExists = await _context.Comments
                .AsNoTracking()
                .AnyAsync(c => c.Id == dto.ParentId.Value && c.ConfessionId == confessionId, cancellationToken);

            if (!parentExists)
            {
                return BadRequest(new { message = "Parent comment does not exist under this confession." });
            }
        }

        var now = DateTime.UtcNow;

        var comment = new Comment
        {
            ConfessionId = confessionId,
            UserId = userId,
            ParentId = dto.ParentId,
            Body = dto.Body.Trim(),
            IsAnonymous = dto.IsAnonymous,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Extract and map @mentions
        var matches = Regex.Matches(dto.Body, @"@([a-zA-Z0-9_]+)");
        var usernames = matches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (usernames.Count > 0)
        {
            var mentionedUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => usernames.Contains(u.Username.ToLower()))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var mentionedId in mentionedUserIds)
            {
                comment.Mentions.Add(new Mention
                {
                    Comment = comment,
                    MentionedUserId = mentionedId,
                    CreatedAt = now
                });
            }
        }

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);

        var result = new CommentDto(
            comment.Id,
            comment.ConfessionId,
            comment.ParentId,
            comment.Body,
            comment.IsAnonymous,
            comment.IsAnonymous ? "Anonymous" : user.Name,
            comment.IsAnonymous ? "anonymous" : user.Username,
            0,
            comment.CreatedAt,
            Array.Empty<CommentDto>()
        );

        return CreatedAtAction(nameof(GetComments), new { confessionId }, result);
    }

    // DELETE: api/v1/comments/5
    [Authorize]
    [HttpDelete("comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (comment is null)
        {
            return NotFound(new { message = $"Comment with ID {id} was not found." });
        }

        var isAuthor = comment.UserId == userId;
        var isAdmin = userRoleClaim is nameof(UserRole.Admin) or nameof(UserRole.SuperAdmin);

        if (!isAuthor && !isAdmin)
        {
            return Forbid();
        }

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}