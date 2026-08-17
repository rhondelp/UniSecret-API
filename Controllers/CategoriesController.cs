using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Services;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CategoriesController : ControllerBase
{
    private const string CacheKey = "categories:all";

    private readonly AppDbContext _context;
    private readonly CacheService _cache;

    public CategoriesController(AppDbContext context, CacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    // GET: api/v1/categories
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<List<CategoryDto>>(CacheKey, cancellationToken);
        if (cached is not null)
        {
            return Ok(cached);
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug
            ))
            .ToListAsync(cancellationToken);

        await _cache.SetAsync(
            CacheKey,
            categories,
            distributedExpiration: TimeSpan.FromHours(1),
            memoryExpiration: TimeSpan.FromMinutes(10),
            cancellationToken);

        return Ok(categories);
    }

    // GET: api/v1/categories/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(
        int id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return NotFound(new { message = $"Category with ID {id} was not found." });
        }

        return Ok(category);
    }
}