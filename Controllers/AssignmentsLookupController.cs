using AssetTracker.Data;
using AssetTracker.Models;
using AssetTracker.Models.Assignments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/lookups")]
public class AssignmentsLookupController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AssignmentsLookupController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("assets")]
    public async Task<IActionResult> Assets([FromQuery] string? q)
    {
        var term = q?.Trim().ToLower();
        var activeAssetIds = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Active
                        || a.Status == AssignmentStatus.Accepted
                        || a.Status == AssignmentStatus.PendingAcceptance
                        || a.Status == AssignmentStatus.ReturnRequested)
            .Select(a => a.AssetId)
            .Distinct()
            .ToListAsync();

        var query = _context.Assets
            .AsNoTracking()
            .Where(a => a.Status == AssetStatus.InStock && !activeAssetIds.Contains(a.Id));

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(a =>
                a.AssetTag.ToLower().Contains(term) ||
                a.SerialNumber.ToLower().Contains(term) ||
                a.Brand.ToLower().Contains(term) ||
                a.Model.ToLower().Contains(term) ||
                a.AssetType.ToString().ToLower().Contains(term));
        }

        var orderedQuery = string.IsNullOrWhiteSpace(term)
            ? query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            : query.OrderBy(a => a.AssetTag);

        var items = await orderedQuery
            .Take(20)
            .Select(a => new
            {
                id = a.Id,
                label = $"{a.AssetTag} - {a.Brand} {a.Model} ({a.SerialNumber})"
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("staff")]
    public async Task<IActionResult> Staff([FromQuery] string? q)
    {
        var term = q?.Trim().ToLower();
        var query = _context.StaffProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(s =>
                s.FullName.ToLower().Contains(term) ||
                s.EmployeeNumber.ToLower().Contains(term) ||
                s.Department.ToLower().Contains(term) ||
                s.PhoneNumber.ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(s => s.FullName)
            .Take(20)
            .Select(s => new
            {
                id = s.Id,
                label = $"{s.FullName} ({s.EmployeeNumber}) - {s.Department}"
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments([FromQuery] string? q)
    {
        var term = q?.Trim().ToLower();

        var query = _context.StaffProfiles
            .AsNoTracking()
            .Where(s => !string.IsNullOrWhiteSpace(s.Department))
            .Select(s => s.Department.Trim())
            .Distinct();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(d => d.ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(d => d)
            .Take(20)
            .Select(d => new { value = d, label = d })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations([FromQuery] string? q)
    {
        var term = q?.Trim().ToLower();
        var query = _context.Assets
            .AsNoTracking()
            .Where(a => !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => a.Location.Trim())
            .Distinct();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(l => l.ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(l => l)
            .Take(20)
            .Select(l => new { value = l, label = l })
            .ToListAsync();

        return Ok(items);
    }
}
