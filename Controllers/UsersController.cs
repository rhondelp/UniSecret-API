using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/users/search?q=john
    [Authorize]
    [HttpGet("search")]
    public async Task<ActionResult<List<UserMentionDto>>> SearchUsers(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new List<UserMentionDto>());
        }

        var searchTerm = q.Trim().ToLowerInvariant().TrimStart('@');
        limit = Math.Clamp(limit, 1, 20);

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Username, $"{searchTerm}%") || EF.Functions.ILike(u.Name, $"%{searchTerm}%"))
            .OrderBy(u => u.Name)
            .Take(limit)
            .Select(u => new UserMentionDto(u.Id, u.Name, u.Username, u.AvatarUrl))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    // GET: api/v1/users/5/profile
    [HttpGet("{id:int}/profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(
        int id,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserProfileDto(
                u.Id,
                u.Name,
                u.Username,
                u.AvatarUrl,
                u.UniversityId,
                u.University.Name,
                u.CreatedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return NotFound(new { message = $"User with ID {id} was not found." });
        }

        return Ok(profile);
    }

    // PUT: api/v1/users/me
    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        UpdateProfileDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        // --------------------------------------------------------
        // Name
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Trim() != user.Name)
        {
            var name = dto.Name.Trim();

            if (name.Length > 100)
            {
                return BadRequest(new { message = "Name must not exceed 100 characters." });
            }

            user.Name = name;
        }

        // --------------------------------------------------------
        // Username (unique, lowercase)
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            var username = dto.Username.Trim().ToLowerInvariant();

            if (username != user.Username)
            {
                if (username.Length > 50)
                {
                    return BadRequest(new { message = "Username must not exceed 50 characters." });
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
                {
                    return BadRequest(new { message = "Username may only contain letters, numbers and underscores." });
                }

                var usernameTaken = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id != userId && u.Username == username, cancellationToken);

                if (usernameTaken)
                {
                    return BadRequest(new { message = "Username is already taken." });
                }

                user.Username = username;
            }
        }

        // --------------------------------------------------------
        // Email (unique + must match university domain)
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            if (email != user.Email)
            {
                var emailTaken = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id != userId && u.Email == email, cancellationToken);

                if (emailTaken)
                {
                    return BadRequest(new { message = "Email is already registered." });
                }

                var domain = await _context.Universities
                    .AsNoTracking()
                    .Where(un => un.Id == user.UniversityId)
                    .Select(un => un.Domain)
                    .FirstOrDefaultAsync(cancellationToken);

                var emailDomain = email.Split('@').Last();

                if (domain is null ||
                    !emailDomain.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = $"Email domain ({emailDomain}) does not match the university's domain ({domain})."
                    });
                }

                user.Email = email;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "The email or username is already taken." });
        }

        return Ok(new
        {
            message = "Profile updated successfully.",
            data = new
            {
                id = user.Id,
                name = user.Name,
                username = user.Username,
                email = user.Email,
                user.UniversityId
            }
        });
    }
}

public record UserMentionDto(int Id, string Name, string Username, string? AvatarUrl);
