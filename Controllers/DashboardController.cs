using AssetTracker.Data;
using AssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        var totalAssets = await _context.Assets.AsNoTracking().CountAsync();
        var inStock = await _context.Assets.AsNoTracking().CountAsync(a => a.Status == AssetStatus.InStock);
        var assigned = await _context.Assets.AsNoTracking().CountAsync(a => a.Status == AssetStatus.Assigned);
        var inRepair = await _context.Assets.AsNoTracking().CountAsync(a => a.Status == AssetStatus.InRepair);

        var openIssuesQuery = _context.Issues
            .AsNoTracking()
            .Where(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);

        var openIssues = await openIssuesQuery.CountAsync();
        // This codebase has Low/Medium/High. Treat High as Critical in dashboard KPI.
        var criticalIssues = await openIssuesQuery.CountAsync(i => i.Priority == IssuePriority.High);

        var allocationRaw = await (
            from assignment in _context.Assignments.AsNoTracking()
            join staff in _context.StaffProfiles.AsNoTracking() on assignment.StaffProfileId equals staff.Id
            where assignment.Status == AssignmentStatus.Active
                  || assignment.Status == AssignmentStatus.PendingAcceptance
                  || assignment.Status == AssignmentStatus.Accepted
                  || assignment.Status == AssignmentStatus.ReturnRequested
            group assignment by (string.IsNullOrWhiteSpace(staff.Department) ? "Unassigned" : staff.Department) into g
            orderby g.Count() descending
            select new
            {
                DepartmentName = g.Key,
                Count = g.Count()
            })
            .Take(6)
            .ToListAsync();

        var totalAllocated = allocationRaw.Sum(x => x.Count);
        var allocation = allocationRaw
            .Select(x => new DepartmentAllocationVm
            {
                DepartmentName = x.DepartmentName,
                Count = x.Count,
                Percent = totalAllocated == 0 ? 0 : (int)Math.Round((x.Count * 100.0) / totalAllocated)
            })
            .ToList();

        var latestOpenIssues = await (
            from issue in _context.Issues.AsNoTracking()
            join asset in _context.Assets.AsNoTracking() on issue.AssetId equals asset.Id into assetJoin
            from asset in assetJoin.DefaultIfEmpty()
            where issue.Status != IssueStatus.Resolved && issue.Status != IssueStatus.Closed
            orderby issue.CreatedAt descending, issue.Id descending
            select new OpenIssueVm
            {
                Id = issue.Id,
                Title = issue.Title,
                Priority = issue.Priority,
                AssetTag = asset != null ? asset.AssetTag : "No Asset",
                CreatedAt = issue.CreatedAt
            })
            .Take(5)
            .ToListAsync();

        var recentAssignments = await (
            from assignment in _context.Assignments.AsNoTracking()
            join asset in _context.Assets.AsNoTracking() on assignment.AssetId equals asset.Id
            join staff in _context.StaffProfiles.AsNoTracking() on assignment.StaffProfileId equals staff.Id
            orderby assignment.AssignedAt descending, assignment.Id descending
            select new ActivityVm
            {
                Type = "assignment",
                Message = $"\"{asset.Brand} {asset.Model}\" assigned to {staff.FullName}",
                OccurredAt = assignment.AssignedAt
            })
            .Take(5)
            .ToListAsync();

        var recentIssues = await (
            from issue in _context.Issues.AsNoTracking()
            join asset in _context.Assets.AsNoTracking() on issue.AssetId equals asset.Id into assetJoin
            from asset in assetJoin.DefaultIfEmpty()
            orderby issue.CreatedAt descending, issue.Id descending
            select new ActivityVm
            {
                Type = "issue",
                Message = asset != null
                    ? $"Issue filed on {asset.AssetTag}"
                    : $"Issue filed: {issue.Title}",
                OccurredAt = issue.CreatedAt
            })
            .Take(5)
            .ToListAsync();

        var recentAssets = await _context.Assets
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new ActivityVm
            {
                Type = "asset",
                Message = $"{a.AssetTag} ({a.Brand} {a.Model}) added to inventory",
                OccurredAt = a.CreatedAt
            })
            .Take(5)
            .ToListAsync();

        var recentActivity = recentAssignments
            .Concat(recentIssues)
            .Concat(recentAssets)
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .ToList();

        var vm = new DashboardVm
        {
            TotalAssets = totalAssets,
            InStock = inStock,
            Assigned = assigned,
            InRepair = inRepair,
            OpenIssues = openIssues,
            CriticalIssues = criticalIssues,
            AllocationByDepartment = allocation,
            LatestOpenIssues = latestOpenIssues,
            RecentActivity = recentActivity
        };

        return View(vm);
    }
}
