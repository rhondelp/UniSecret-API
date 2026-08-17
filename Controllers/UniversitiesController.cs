using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;
using UniSecretApi.Enums;
using UniSecretApi.Services;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UniversitiesController : ControllerBase
{
    private const string CacheKey = "universities:all";
    private readonly AppDbContext _context;
    private readonly CacheService _cache;

    public UniversitiesController(AppDbContext context, CacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // GET: api/v1/universities
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UniversityDto>>> GetUniversities(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<List<UniversityDto>>(CacheKey, cancellationToken);
        if (cached is not null) return Ok(cached);

        var universities = await _context.Universities
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UniversityDto(u.Id, u.Name, u.Domain, u.LogoUrl, u.Status, u.CreatedAt))
            .ToListAsync(cancellationToken);

        await _cache.SetAsync(CacheKey, universities, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), cancellationToken);
        return Ok(universities);
    }

    // GET: api/v1/universities/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UniversityDto>> GetUniversity(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"university:{id}";
        var cached = await _cache.GetAsync<UniversityDto>(cacheKey, cancellationToken);
        if (cached is not null) return Ok(cached);

        var university = await _context.Universities
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UniversityDto(u.Id, u.Name, u.Domain, u.LogoUrl, u.Status, u.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (university is null)
        {
            return NotFound(new { message = $"University with ID {id} was not found." });
        }

        await _cache.SetAsync(cacheKey, university, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), cancellationToken);
        return Ok(university);
    }

    // POST: api/v1/universities
    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<UniversityDto>> CreateUniversity(CreateUniversityDto dto, CancellationToken cancellationToken = default)
    {
        var domain = dto.Domain.Trim().ToLowerInvariant();
        var domainExists = await _context.Universities.AsNoTracking().AnyAsync(u => u.Domain == domain, cancellationToken);

        if (domainExists)
        {
            return BadRequest(new { message = "A university with this domain already exists." });
        }

        var now = DateTime.UtcNow;
        var university = new University
        {
            Name = dto.Name.Trim(),
            Domain = domain,
            LogoUrl = dto.LogoUrl,
            Status = UniversityStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Universities.Add(university);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(CacheKey, cancellationToken);
        var resultDto = new UniversityDto(university.Id, university.Name, university.Domain, university.LogoUrl, university.Status, university.CreatedAt);
        await _cache.SetAsync($"university:{university.Id}", resultDto, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30), cancellationToken);

        return CreatedAtAction(nameof(GetUniversity), new { id = university.Id }, resultDto);
    }

    // PUT: api/v1/universities/5
    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUniversity(int id, UpdateUniversityDto dto, CancellationToken cancellationToken = default)
    {
        var university = await _context.Universities.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (university is null) return NotFound(new { message = $"University with ID {id} was not found." });

        var domain = dto.Domain.Trim().ToLowerInvariant();
        var domainExists = await _context.Universities.AsNoTracking().AnyAsync(u => u.Id != id && u.Domain == domain, cancellationToken);
        if (domainExists) return BadRequest(new { message = "A university with this domain already exists." });

        university.Name = dto.Name.Trim();
        university.Domain = domain;
        university.LogoUrl = dto.LogoUrl;
        university.Status = dto.Status;
        university.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey, cancellationToken);
        await _cache.RemoveAsync($"university:{id}", cancellationToken);

        return NoContent();
    }

    // DELETE: api/v1/universities/5
    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUniversity(int id, CancellationToken cancellationToken = default)
    {
        var university = await _context.Universities.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (university is null) return NotFound(new { message = $"University with ID {id} was not found." });

        _context.Universities.Remove(university);
        await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey, cancellationToken);
        await _cache.RemoveAsync($"university:{id}", cancellationToken);

        return NoContent();
    }
}