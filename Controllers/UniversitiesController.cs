using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;
using UniSecretApi.Enums;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UniversitiesController : ControllerBase
{
    private readonly AppDbContext _context;

    public UniversitiesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/universities
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UniversityDto>>> GetUniversities()
    {
        var universities = await _context.Universities
            .AsNoTracking()
            .Select(u => new UniversityDto(
                u.Id,
                u.Name,
                u.Domain,
                u.LogoUrl,
                u.Status,
                u.CreatedAt
            ))
            .ToListAsync();

        return Ok(universities);
    }

    // GET: api/v1/universities/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UniversityDto>> GetUniversity(int id)
    {
        var university = await _context.Universities.FindAsync(id);

        if (university is null)
        {
            return NotFound(new { message = $"University with ID {id} was not found." });
        }

        var dto = new UniversityDto(
            university.Id,
            university.Name,
            university.Domain,
            university.LogoUrl,
            university.Status,
            university.CreatedAt
        );

        return Ok(dto);
    }

    // POST: api/v1/universities
    [HttpPost]
    public async Task<ActionResult<UniversityDto>> CreateUniversity(CreateUniversityDto dto)
    {
        // Check if domain is already registered
        bool domainExists = await _context.Universities
            .AnyAsync(u => u.Domain.ToLower() == dto.Domain.ToLower());

        if (domainExists)
        {
            return BadRequest(new { message = "A university with this domain already exists." });
        }

        var university = new University
        {
            Name = dto.Name,
            Domain = dto.Domain.ToLower(),
            LogoUrl = dto.LogoUrl,
            Status = UniversityStatus.Pending, // Defaults to pending
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Universities.Add(university);
        await _context.SaveChangesAsync();

        var resultDto = new UniversityDto(
            university.Id,
            university.Name,
            university.Domain,
            university.LogoUrl,
            university.Status,
            university.CreatedAt
        );

        return CreatedAtAction(nameof(GetUniversity), new { id = university.Id }, resultDto);
    }

    // PUT: api/v1/universities/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUniversity(int id, UpdateUniversityDto dto)
    {
        var university = await _context.Universities.FindAsync(id);

        if (university is null)
        {
            return NotFound(new { message = $"University with ID {id} was not found." });
        }

        university.Name = dto.Name;
        university.Domain = dto.Domain.ToLower();
        university.LogoUrl = dto.LogoUrl;
        university.Status = dto.Status;
        university.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/v1/universities/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUniversity(int id)
    {
        var university = await _context.Universities.FindAsync(id);

        if (university is null)
        {
            return NotFound(new { message = $"University with ID {id} was not found." });
        }

        _context.Universities.Remove(university);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}