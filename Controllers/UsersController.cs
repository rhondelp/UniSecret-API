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
}

public record UserMentionDto(int Id, string Name, string Username, string? AvatarUrl);