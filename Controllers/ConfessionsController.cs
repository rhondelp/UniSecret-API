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

    // ============================================================
    // GET CONFESSIONS
    // ============================================================

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

        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _ = int.TryParse(
            userIdClaim,
            out var currentUserId);

        var query = _context.Confessions
            .AsNoTracking()
            .Where(c =>
                c.Status == ConfessionStatus.Approved);

        if (universityId.HasValue)
        {
            query = query.Where(
                c => c.UniversityId == universityId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(
                c => c.CategoryId == categoryId.Value);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var confessions =
            await query
                .OrderByDescending(
                    c => c.CreatedAt)
                .ThenByDescending(
                    c => c.Id)
                .Skip(
                    (page - 1) * pageSize)
                .Take(pageSize)
                .Select(c =>
                    new ConfessionDto(
                        c.Id,
                        c.UniversityId,
                        c.CategoryId,
                        c.Category.Name,
                        c.Body,
                        c.IsAnonymous,

                        c.IsAnonymous
                            ? "Anonymous"
                            : (
                                string.IsNullOrWhiteSpace(
                                    c.User.Name)
                                    ? "Anonymous User"
                                    : c.User.Name
                            ),

                        c.IsAnonymous
                            ? "anonymous"
                            : c.User.Username,

                        c.Status,
                        c.ScheduledAt,
                        c.CreatedAt,

                        _context.Reactions.Count(
                            r =>
                                r.ReactableId == c.Id &&
                                r.ReactableType == "Confession"),

                        currentUserId > 0 &&
                        _context.Reactions.Any(
                            r =>
                                r.UserId == currentUserId &&
                                r.ReactableId == c.Id &&
                                r.ReactableType == "Confession"),

                        currentUserId > 0 &&
                        _context.SavedPosts.Any(
                            s =>
                                s.UserId == currentUserId &&
                                s.ConfessionId == c.Id),

                        c.ImageUrl,

                        _context.Comments.Count(
                            cm =>
                                cm.ConfessionId == c.Id),

                        _context.Shares.Count(
                            sh =>
                                sh.ConfessionId == c.Id),

                        currentUserId > 0
                            ? _context.Reactions
                                .Where(
                                    r =>
                                        r.UserId == currentUserId &&
                                        r.ReactableId == c.Id &&
                                        r.ReactableType == "Confession")
                                .Select(
                                    r => (ReactionType?)r.Type)
                                .FirstOrDefault()
                            : null
                    ))
                .ToListAsync(
                    cancellationToken);

        var totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize);

        return Ok(
            new PagedResult<ConfessionDto>(
                confessions,
                page,
                pageSize,
                totalCount,
                totalPages));
    }

    // ============================================================
    // SEARCH CONFESSIONS
    // ============================================================

    // GET: api/v1/confessions/search?q=exam&universityId=1&categoryId=2
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ConfessionDto>>> SearchConfessions(
        [FromQuery] ConfessionSearchQueryDto queryDto,
        CancellationToken cancellationToken = default)
    {
        var page =
            Math.Max(
                queryDto.Page,
                1);

        var pageSize =
            Math.Clamp(
                queryDto.PageSize,
                1,
                100);

        var query =
            _context.Confessions
                .AsNoTracking()
                .Where(
                    c =>
                        c.Status ==
                        ConfessionStatus.Approved);

        if (!string.IsNullOrWhiteSpace(
                queryDto.Q))
        {
            var searchTerm =
                queryDto.Q.Trim();

            query =
                query.Where(
                    c =>
                        EF.Functions.ILike(
                            c.Body,
                            $"%{searchTerm}%"));
        }

        if (queryDto.UniversityId.HasValue)
        {
            query =
                query.Where(
                    c =>
                        c.UniversityId ==
                        queryDto.UniversityId.Value);
        }

        if (queryDto.CategoryId.HasValue)
        {
            query =
                query.Where(
                    c =>
                        c.CategoryId ==
                        queryDto.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                queryDto.Tag))
        {
            var normalizedTag =
                queryDto.Tag
                    .Trim()
                    .ToLowerInvariant()
                    .TrimStart('#');

            query =
                query.Where(
                    c =>
                        c.ConfessionHashtags.Any(
                            ch =>
                                ch.Hashtag.Tag ==
                                normalizedTag));
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var confessions =
            await query
                .OrderByDescending(
                    c => c.CreatedAt)
                .ThenByDescending(
                    c => c.Id)
                .Skip(
                    (page - 1) * pageSize)
                .Take(pageSize)
                .Select(
                    c =>
                        new ConfessionDto(
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
                .ToListAsync(
                    cancellationToken);

        var totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize);

        return Ok(
            new PagedResult<ConfessionDto>(
                confessions,
                page,
                pageSize,
                totalCount,
                totalPages));
    }

    // ============================================================
    // GET SINGLE CONFESSION
    // ============================================================

    // GET: api/v1/confessions/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConfessionDto>> GetConfession(
        int id,
        CancellationToken cancellationToken = default)
    {
        var confession =
            await _context.Confessions
                .AsNoTracking()
                .Where(
                    c => c.Id == id)
                .Select(
                    c =>
                        new
                        {
                            c.Id,
                            c.UserId,
                            c.UniversityId,
                            c.CategoryId,
                            CategoryName =
                                c.Category.Name,
                            c.Body,
                            c.IsAnonymous,
                            AuthorName =
                                c.IsAnonymous
                                    ? "Anonymous"
                                    : c.User.Name,
                            AuthorUsername =
                                c.IsAnonymous
                                    ? "anonymous"
                                    : c.User.Username,
                            c.Status,
                            c.ScheduledAt,
                            c.CreatedAt
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (confession is null)
        {
            return NotFound(
                new
                {
                    message =
                        $"Confession with ID {id} was not found."
                });
        }

        if (confession.Status !=
            ConfessionStatus.Approved)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)
                ?.Value;

            var userRoleClaim =
                User.FindFirst(
                    ClaimTypes.Role)
                ?.Value;

            var isAuthor =
                userIdClaim != null &&
                int.TryParse(
                    userIdClaim,
                    out var userId) &&
                userId ==
                    confession.UserId;

            var isAdmin =
                userRoleClaim is
                    nameof(UserRole.Admin) or
                    nameof(UserRole.SuperAdmin);

            if (!isAuthor && !isAdmin)
            {
                return NotFound(
                    new
                    {
                        message =
                            $"Confession with ID {id} was not found."
                    });
            }
        }

        var dto =
            new ConfessionDto(
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

    // ============================================================
    // CREATE CONFESSION
    // ============================================================

    // POST: api/v1/confessions
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ConfessionDto>> CreateConfession(
        [FromBody] CreateConfessionDto dto,
        CancellationToken cancellationToken = default)
    {
        // --------------------------------------------------------
        // 1. Get authenticated user ID from JWT
        // --------------------------------------------------------

        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value;

        if (string.IsNullOrWhiteSpace(
                userIdClaim) ||
            !int.TryParse(
                userIdClaim,
                out var userId))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Invalid token claims."
                });
        }

        // --------------------------------------------------------
        // 2. Get the USER'S university directly from DB
        //
        // Do NOT trust universityId sent by the mobile app.
        // --------------------------------------------------------

        var user =
            await _context.Users
                .AsNoTracking()
                .Where(
                    u => u.Id == userId)
                .Select(
                    u =>
                        new
                        {
                            u.Id,
                            u.Status,
                            u.UniversityId
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (user is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "User record not found."
                });
        }

        // --------------------------------------------------------
        // 3. Verify active account
        // --------------------------------------------------------

        if (user.Status !=
            UserStatus.Active)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        $"Your account is {user.Status.ToString().ToLowerInvariant()}. Contact support."
                });
        }

        // --------------------------------------------------------
        // 4. Validate user's university
        //
        // UniversityId comes from Users.UniversityId.
        // The DTO's UniversityId is intentionally ignored.
        // --------------------------------------------------------

        if (user.UniversityId <= 0)
        {
            return BadRequest(
                new
                {
                    message =
                        "Your account is not associated with a university."
                });
        }

        var university =
            await _context.Universities
                .AsNoTracking()
                .Where(
                    u =>
                        u.Id ==
                        user.UniversityId)
                .Select(
                    u =>
                        new
                        {
                            u.Id,
                            u.Name,
                            u.Status
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (university is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "The university associated with your account could not be found."
                });
        }

        // --------------------------------------------------------
        // 5. Validate body
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                dto.Body))
        {
            return BadRequest(
                new
                {
                    message =
                        "Confession body is required."
                });
        }

        var body =
            dto.Body.Trim();

        if (body.Length > 2000)
        {
            return BadRequest(
                new
                {
                    message =
                        "Confession must not exceed 2000 characters."
                });
        }

        // --------------------------------------------------------
        // 6. Validate category
        // --------------------------------------------------------

        var categoryExists =
            await _context.Categories
                .AsNoTracking()
                .AnyAsync(
                    c =>
                        c.Id ==
                        dto.CategoryId,
                    cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(
                new
                {
                    message =
                        "Category not found."
                });
        }

        // --------------------------------------------------------
        // 7. Create confession
        // --------------------------------------------------------

        var now =
            DateTime.UtcNow;

        var confession =
            new Confession
            {
                UserId =
                    userId,

                // IMPORTANT:
                // Always use the authenticated user's university.
                UniversityId =
                    user.UniversityId,

                CategoryId =
                    dto.CategoryId,

                Body =
                    body,

                IsAnonymous =
                    dto.IsAnonymous,

                ImageUrl =
                    dto.ImageUrl,

                Status =
                    ConfessionStatus.Pending,

                ScheduledAt =
                    dto.ScheduledAt,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        // --------------------------------------------------------
        // 8. Extract hashtags
        // --------------------------------------------------------

        var matches =
            Regex.Matches(
                body,
                @"#([a-zA-Z0-9_]+)");

        var tags =
            matches
                .Select(
                    m =>
                        m.Groups[1]
                            .Value
                            .ToLowerInvariant())
                .Distinct()
                .Where(
                    t =>
                        t.Length <= 50)
                .ToList();

        if (tags.Count > 0)
        {
            var existingHashtags =
                await _context.Hashtags
                    .Where(
                        h =>
                            tags.Contains(
                                h.Tag))
                    .ToListAsync(
                        cancellationToken);

            var existingTagsMap =
                existingHashtags
                    .ToDictionary(
                        h => h.Tag,
                        h => h);

            foreach (var tag in tags)
            {
                if (!existingTagsMap.TryGetValue(
                        tag,
                        out var hashtag))
                {
                    hashtag =
                        new Hashtag
                        {
                            Tag = tag
                        };

                    _context.Hashtags.Add(
                        hashtag);

                    existingTagsMap[tag] =
                        hashtag;
                }

                confession
                    .ConfessionHashtags
                    .Add(
                        new ConfessionHashtag
                        {
                            Confession =
                                confession,

                            Hashtag =
                                hashtag
                        });
            }
        }

        // --------------------------------------------------------
        // 9. Save
        // --------------------------------------------------------

        _context.Confessions.Add(
            confession);

        await _context.SaveChangesAsync(
            cancellationToken);

        // --------------------------------------------------------
        // 10. Return useful information
        // --------------------------------------------------------

        return Ok(
            new
            {
                message =
                    "Confession submitted successfully and is pending review.",

                id =
                    confession.Id,

                universityId =
                    confession.UniversityId
            });
    }
}