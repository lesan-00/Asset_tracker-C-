using AssetTracker.Data;
using AssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class SearchController : Controller
{
    private readonly ApplicationDbContext _context;

    public SearchController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Results(string? q)
    {
        ViewData["Title"] = "Search Results";
        var query = q?.Trim() ?? string.Empty;

        var vm = new SearchResultsVm { Query = query };

        if (string.IsNullOrWhiteSpace(query))
        {
            return View(vm);
        }

        var lowered = query.ToLower();

        vm.Assets = await _context.Assets
            .AsNoTracking()
            .Where(a =>
                a.AssetTag.ToLower().Contains(lowered) ||
                a.SerialNumber.ToLower().Contains(lowered) ||
                a.Brand.ToLower().Contains(lowered) ||
                a.Model.ToLower().Contains(lowered))
            .OrderBy(a => a.AssetTag)
            .Take(25)
            .Select(a => new SearchAssetRowVm
            {
                Id = a.Id,
                AssetTag = a.AssetTag,
                SerialNumber = a.SerialNumber,
                Display = $"{a.Brand} {a.Model}",
                Status = a.Status.ToString()
            })
            .ToListAsync();

        vm.Staff = await _context.StaffProfiles
            .AsNoTracking()
            .Where(s =>
                s.FullName.ToLower().Contains(lowered) ||
                s.EmployeeNumber.ToLower().Contains(lowered) ||
                s.Department.ToLower().Contains(lowered) ||
                s.PhoneNumber.ToLower().Contains(lowered))
            .OrderBy(s => s.FullName)
            .Take(25)
            .Select(s => new SearchStaffRowVm
            {
                Id = s.Id,
                FullName = s.FullName,
                EmployeeNumber = s.EmployeeNumber,
                Department = s.Department,
                PhoneNumber = s.PhoneNumber
            })
            .ToListAsync();

        return View(vm);
    }
}
