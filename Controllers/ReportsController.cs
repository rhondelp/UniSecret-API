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
[Authorize]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/v1/reports
    [HttpPost]
    public async Task<IActionResult> CreateReport(
        CreateReportDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var normalizedType = dto.ReportableType.Trim();
        if (normalizedType is not ("Confession" or "Comment"))
        {
            return BadRequest(new { message = "ReportableType must be 'Confession' or 'Comment'." });
        }

        if (!Enum.TryParse<ReportReason>(dto.Reason.Trim(), true, out var parsedReason))
        {
            return BadRequest(new { message = "Invalid report reason provided." });
        }

        if (normalizedType == "Confession")
        {
            var exists = await _context.Confessions.AsNoTracking().AnyAsync(c => c.Id == dto.ReportableId, cancellationToken);
            if (!exists) return NotFound(new { message = "Confession not found." });
        }
        else
        {
            var exists = await _context.Comments.AsNoTracking().AnyAsync(c => c.Id == dto.ReportableId, cancellationToken);
            if (!exists) return NotFound(new { message = "Comment not found." });
        }

        var now = DateTime.UtcNow;
        var report = new Report
        {
            ReporterId = userId,
            ReportableId = dto.ReportableId,
            ReportableType = normalizedType,
            Reason = parsedReason,
            Status = ReportStatus.Pending,
            CreatedAt = now
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Report submitted successfully.", reportId = report.Id });
    }

    // GET: api/v1/reports?page=1&pageSize=20
    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ReportDto>>> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Reports
            .AsNoTracking()
            .Where(r => r.Status == ReportStatus.Pending);

        var totalCount = await query.CountAsync(cancellationToken);

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReportDto(
                r.Id,
                r.ReporterId,
                r.Reporter.Username,
                r.ReportableId,
                r.ReportableType,
                r.Reason.ToString(),
                r.Status.ToString(),
                r.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResult<ReportDto>(reports, page, pageSize, totalCount, totalPages));
    }
}