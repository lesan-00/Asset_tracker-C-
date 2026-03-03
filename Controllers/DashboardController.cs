using AssetTracker.Data;
using AssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var dashboardViewModel = new DashboardViewModel
        {
            TotalAssets = await _context.Assets.CountAsync(),
            InStock = await _context.Assets.CountAsync(a => a.Status == AssetStatus.InStock),
            Assigned = await _context.Assets.CountAsync(a => a.Status == AssetStatus.Assigned),
            InRepair = await _context.Assets.CountAsync(a => a.Status == AssetStatus.InRepair),
            OpenIssues = await _context.Issues.CountAsync(i => i.Status == IssueStatus.Open),
            Query = q?.Trim()
        };

        if (string.IsNullOrWhiteSpace(dashboardViewModel.Query))
        {
            return View(dashboardViewModel);
        }

        var normalizedQuery = dashboardViewModel.Query.ToLower();
        var activeAssignmentStatuses = new List<AssignmentStatus>
        {
            AssignmentStatus.Active,
            AssignmentStatus.PendingAcceptance,
            AssignmentStatus.Accepted,
            AssignmentStatus.ReturnRequested
        };

        var latestAssetAssignments = await _context.Assignments
            .AsNoTracking()
            .Where(a => activeAssignmentStatuses.Contains(a.Status))
            .Include(a => a.StaffProfile)
            .GroupBy(a => a.AssetId)
            .Select(g => g.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id).Select(x => new
            {
                x.AssetId,
                x.Status,
                StaffName = x.StaffProfile.FullName
            }).FirstOrDefault())
            .ToDictionaryAsync(x => x!.AssetId, x => x!);

        var matchingTypes = Enum.GetValues<AssetType>()
            .Where(t => t.ToString().ToLower().Contains(normalizedQuery) || GetAssetTypeDisplayName(t).ToLower().Contains(normalizedQuery))
            .ToList();

        var assets = await _context.Assets
            .AsNoTracking()
            .Where(a =>
                a.AssetTag.ToLower().Contains(normalizedQuery) ||
                a.SerialNumber.ToLower().Contains(normalizedQuery) ||
                a.Brand.ToLower().Contains(normalizedQuery) ||
                a.Model.ToLower().Contains(normalizedQuery) ||
                matchingTypes.Contains(a.AssetType))
            .OrderBy(a => a.AssetTag)
            .Take(20)
            .ToListAsync();

        dashboardViewModel.Results.AddRange(assets.Select(a =>
        {
            latestAssetAssignments.TryGetValue(a.Id, out var currentAssignment);
            var statusText = currentAssignment is null
                ? a.Status.ToString()
                : $"{a.Status} - Assigned to {currentAssignment.StaffName}";

            return new SearchResultRow
            {
                Type = "Asset",
                Title = a.AssetTag,
                SubTitle = $"{a.Brand} {a.Model} | SN: {a.SerialNumber} | Type: {GetAssetTypeDisplayName(a.AssetType)}",
                StatusText = statusText,
                LinkUrl = Url.Action("Details", "Assets", new { id = a.Id }) ?? $"/Assets/Details/{a.Id}"
            };
        }));

        if (User.IsInRole("Admin"))
        {
            var staffAssignments = await _context.Assignments
                .AsNoTracking()
                .Where(a => activeAssignmentStatuses.Contains(a.Status))
                .Include(a => a.Asset)
                .GroupBy(a => a.StaffProfileId)
                .Select(g => g.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id).Select(x => new
                {
                    x.StaffProfileId,
                    x.Status,
                    AssetTag = x.Asset.AssetTag
                }).FirstOrDefault())
                .ToDictionaryAsync(x => x!.StaffProfileId, x => x!);

            var staff = await _context.StaffProfiles
                .AsNoTracking()
                .Include(s => s.User)
                .Where(s =>
                    s.FullName.ToLower().Contains(normalizedQuery) ||
                    s.EmployeeNumber.ToLower().Contains(normalizedQuery) ||
                    s.PhoneNumber.ToLower().Contains(normalizedQuery) ||
                    (s.User != null && s.User.Email != null && s.User.Email.ToLower().Contains(normalizedQuery)))
                .OrderBy(s => s.FullName)
                .Take(20)
                .ToListAsync();

            dashboardViewModel.Results.AddRange(staff.Select(s =>
            {
                staffAssignments.TryGetValue(s.Id, out var currentAssignment);
                var statusText = currentAssignment is null
                    ? "Active - No active assignment"
                    : $"Active - Assigned: {currentAssignment.AssetTag}";

                return new SearchResultRow
                {
                    Type = "Staff",
                    Title = s.FullName,
                    SubTitle = $"{s.EmployeeNumber} | {s.User?.Email ?? "No Email"}",
                    StatusText = statusText,
                    LinkUrl = Url.Action("Details", "Staff", new { id = s.Id }) ?? $"/Staff/Details/{s.Id}"
                };
            }));
        }
        else
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var self = await _context.StaffProfiles
                    .AsNoTracking()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.UserId == currentUserId);

                if (self is not null &&
                    (self.FullName.ToLower().Contains(normalizedQuery) ||
                     self.EmployeeNumber.ToLower().Contains(normalizedQuery) ||
                     self.PhoneNumber.ToLower().Contains(normalizedQuery) ||
                     ((self.User?.Email ?? string.Empty).ToLower().Contains(normalizedQuery))))
                {
                    var selfCurrentAssignment = await _context.Assignments
                        .AsNoTracking()
                        .Where(a => a.StaffProfileId == self.Id && activeAssignmentStatuses.Contains(a.Status))
                        .Include(a => a.Asset)
                        .OrderByDescending(a => a.AssignedAt)
                        .ThenByDescending(a => a.Id)
                        .Select(a => new { a.Asset.AssetTag })
                        .FirstOrDefaultAsync();

                    dashboardViewModel.Results.Add(new SearchResultRow
                    {
                        Type = "Staff",
                        Title = self.FullName,
                        SubTitle = $"{self.EmployeeNumber} | {self.User?.Email ?? "No Email"}",
                        StatusText = selfCurrentAssignment is null
                            ? "Active - No active assignment"
                            : $"Active - Assigned: {selfCurrentAssignment.AssetTag}",
                        LinkUrl = Url.Action("MyProfile", "Staff") ?? "/Staff/MyProfile"
                    });
                }
            }
        }

        dashboardViewModel.Results = dashboardViewModel.Results
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Title)
            .ToList();

        return View(dashboardViewModel);
    }

    private static string GetAssetTypeDisplayName(AssetType assetType)
    {
        var member = typeof(AssetType).GetMember(assetType.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .FirstOrDefault();
        return display?.Name ?? assetType.ToString();
    }
}
