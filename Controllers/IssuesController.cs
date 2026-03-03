using AssetTracker.Data;
using AssetTracker.Models;
using AssetTracker.Models.Issues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class IssuesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<IssuesController> _logger;

    public IssuesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<IssuesController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, string? priority, bool openCreate = false)
    {
        var normalizedQ = q?.Trim();
        var normalizedStatusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var normalizedPriorityFilter = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();

        var issuesQuery = _context.Issues
            .AsNoTracking()
            .Include(i => i.Asset)
            .Include(i => i.ReportedForStaffProfile)
            .AsQueryable();

        IssueStatus? parsedStatus = null;
        if (string.Equals(normalizedStatusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            // All statuses.
        }
        else if (!string.IsNullOrWhiteSpace(normalizedStatusFilter) &&
                 Enum.TryParse<IssueStatus>(normalizedStatusFilter, true, out var enumStatus))
        {
            parsedStatus = enumStatus;
            issuesQuery = issuesQuery.Where(i => i.Status == enumStatus);
        }
        else
        {
            issuesQuery = issuesQuery.Where(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
        }

        IssuePriority? parsedPriority = null;
        if (!string.IsNullOrWhiteSpace(normalizedPriorityFilter) &&
            !string.Equals(normalizedPriorityFilter, "all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<IssuePriority>(normalizedPriorityFilter, true, out var enumPriority))
        {
            parsedPriority = enumPriority;
            issuesQuery = issuesQuery.Where(i => i.Priority == enumPriority);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            issuesQuery = issuesQuery.Where(i =>
                i.Title.ToLower().Contains(lowered) ||
                i.Description.ToLower().Contains(lowered) ||
                i.Category.ToLower().Contains(lowered) ||
                (i.Asset != null && i.Asset.AssetTag.ToLower().Contains(lowered)) ||
                (i.Asset != null && i.Asset.SerialNumber.ToLower().Contains(lowered)));
        }

        var items = await issuesQuery
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(200)
            .Select(i => new IssueListItemVm
            {
                Id = i.Id,
                Title = i.Title,
                Category = i.Category,
                Description = i.Description,
                Status = i.Status,
                Priority = i.Priority,
                AssetLabel = i.AssetId.HasValue
                    ? $"{i.Asset!.AssetTag} ({i.Asset.SerialNumber})"
                    : "No Asset",
                ReportedForLabel = i.ReportedForStaffProfileId.HasValue
                    ? i.ReportedForStaffProfile!.FullName
                    : "Not specified",
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        var vm = await BuildIndexVmAsync(statusFilter: normalizedStatusFilter, priorityFilter: normalizedPriorityFilter);
        vm.Items = items;
        vm.OpenCount = await _context.Issues
            .AsNoTracking()
            .CountAsync(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
        vm.ResolvedCount = await _context.Issues
            .AsNoTracking()
            .CountAsync(i => i.Status == IssueStatus.Resolved);
        vm.Q = normalizedQ;
        vm.Status = parsedStatus;
        vm.Priority = parsedPriority;
        vm.StatusFilter = normalizedStatusFilter;
        vm.PriorityFilter = normalizedPriorityFilter;
        vm.OpenCreateModal = openCreate;

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return RedirectToAction(nameof(Index), new { openCreate = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "CreateForm")] IssueCreateVm vm)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Challenge();
        }

        if (vm.AssetId.HasValue && !await _context.Assets.AnyAsync(a => a.Id == vm.AssetId.Value))
        {
            ModelState.AddModelError(nameof(IssueCreateVm.AssetId), "Selected asset does not exist.");
        }

        if (vm.ReportedForStaffProfileId.HasValue &&
            !await _context.StaffProfiles.AnyAsync(s => s.Id == vm.ReportedForStaffProfileId.Value))
        {
            ModelState.AddModelError(nameof(IssueCreateVm.ReportedForStaffProfileId), "Selected staff profile does not exist.");
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return BadRequest(new
                {
                    success = false,
                    errors = ModelState
                        .Where(kvp => kvp.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
                });
            }

            var invalidVm = await BuildIndexVmAsync(vm);
            invalidVm.OpenCreateModal = true;
            invalidVm.Items = await LoadDefaultIssueItemsAsync();
            invalidVm.OpenCount = await _context.Issues
                .AsNoTracking()
                .CountAsync(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            invalidVm.ResolvedCount = await _context.Issues
                .AsNoTracking()
                .CountAsync(i => i.Status == IssueStatus.Resolved);
            return View(nameof(Index), invalidVm);
        }

        var issue = new Issue
        {
            Title = vm.Title.Trim(),
            Category = vm.Category.Trim(),
            Description = vm.Description.Trim(),
            Priority = vm.Priority,
            Status = IssueStatus.Open,
            AssetId = vm.AssetId,
            ReportedForStaffProfileId = vm.ReportedForStaffProfileId,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            AssignedToUserId = null
        };

        try
        {
            _context.Issues.Add(issue);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Issue create failed for user {UserId}.", currentUserId);

            if (IsAjaxRequest())
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Unable to save issue right now. Please try again."
                });
            }

            ModelState.AddModelError(string.Empty, "Unable to save issue right now. Please try again.");
            var failedVm = await BuildIndexVmAsync(vm);
            failedVm.OpenCreateModal = true;
            failedVm.Items = await LoadDefaultIssueItemsAsync();
            failedVm.OpenCount = await _context.Issues
                .AsNoTracking()
                .CountAsync(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            failedVm.ResolvedCount = await _context.Issues
                .AsNoTracking()
                .CountAsync(i => i.Status == IssueStatus.Resolved);
            return View(nameof(Index), failedVm);
        }

        if (IsAjaxRequest())
        {
            return Json(new { success = true, issueId = issue.Id });
        }

        TempData["IssueSuccess"] = "Issue submitted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string? q, string? status, string? priority)
    {
        var issue = await _context.Issues.FirstOrDefaultAsync(i => i.Id == id);
        if (issue is null)
        {
            return NotFound();
        }

        issue.Status = IssueStatus.Resolved;
        issue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { q, status, priority });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var issue = await _context.Issues
            .Include(i => i.Asset)
            .Include(i => i.CreatedByUser)
            .Include(i => i.AssignedToUser)
            .Include(i => i.ReportedForStaffProfile)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (issue is null)
        {
            return NotFound();
        }

        return View(issue);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var issue = await _context.Issues.FindAsync(id);
        if (issue is null)
        {
            return NotFound();
        }

        var vm = new IssueEditVm
        {
            Id = issue.Id,
            AssetId = issue.AssetId,
            ReportedForStaffProfileId = issue.ReportedForStaffProfileId,
            Status = issue.Status,
            Priority = issue.Priority,
            AssignedToUserId = issue.AssignedToUserId
        };

        await PopulateAdminEditDropDownsAsync(issue.AssetId, issue.AssignedToUserId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, IssueEditVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateAdminEditDropDownsAsync(vm.AssetId, vm.AssignedToUserId);
            return View(vm);
        }

        var issue = await _context.Issues.FirstOrDefaultAsync(i => i.Id == id);
        if (issue is null)
        {
            return NotFound();
        }

        if (vm.AssetId.HasValue && !await _context.Assets.AnyAsync(a => a.Id == vm.AssetId.Value))
        {
            ModelState.AddModelError(nameof(IssueEditVm.AssetId), "Selected asset does not exist.");
        }

        if (vm.ReportedForStaffProfileId.HasValue &&
            !await _context.StaffProfiles.AnyAsync(s => s.Id == vm.ReportedForStaffProfileId.Value))
        {
            ModelState.AddModelError(nameof(IssueEditVm.ReportedForStaffProfileId), "Selected staff profile does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(vm.AssignedToUserId) &&
            !await _userManager.Users.AnyAsync(u => u.Id == vm.AssignedToUserId))
        {
            ModelState.AddModelError(nameof(IssueEditVm.AssignedToUserId), "Selected assignee does not exist.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAdminEditDropDownsAsync(vm.AssetId, vm.AssignedToUserId);
            return View(vm);
        }

        issue.AssetId = vm.AssetId;
        issue.ReportedForStaffProfileId = vm.ReportedForStaffProfileId;
        issue.Status = vm.Status;
        issue.Priority = vm.Priority;
        issue.AssignedToUserId = vm.AssignedToUserId;
        issue.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Issue edit failed for issue {IssueId}.", id);
            ModelState.AddModelError(string.Empty, "Unable to save changes right now. Please try again.");
            await PopulateAdminEditDropDownsAsync(vm.AssetId, vm.AssignedToUserId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = issue.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var issue = await _context.Issues
            .Include(i => i.Asset)
            .Include(i => i.CreatedByUser)
            .Include(i => i.AssignedToUser)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (issue is null)
        {
            return NotFound();
        }

        return View(issue);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, string? q, string? status, string? priority)
    {
        var issue = await _context.Issues.FindAsync(id);
        if (issue is not null)
        {
            _context.Issues.Remove(issue);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { q, status, priority });
    }

    private async Task<IssuesIndexViewModel> BuildIndexVmAsync(
        IssueCreateVm? createSource = null,
        string? statusFilter = null,
        string? priorityFilter = null)
    {
        var model = new IssuesIndexViewModel
        {
            CreateForm = createSource ?? new IssueCreateVm(),
            StatusFilter = statusFilter,
            PriorityFilter = priorityFilter
        };

        var statusItems = new List<SelectListItem>
        {
            new() { Value = "all", Text = "All Status" }
        };
        statusItems.AddRange(Enum.GetValues<IssueStatus>()
            .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() }));
        model.StatusOptions = new SelectList(statusItems, "Value", "Text", model.StatusFilter);

        var priorityItems = new List<SelectListItem>
        {
            new() { Value = "all", Text = "All Priorities" }
        };
        priorityItems.AddRange(Enum.GetValues<IssuePriority>()
            .Select(p => new SelectListItem { Value = p.ToString(), Text = p.ToString() }));
        model.PriorityOptions = new SelectList(priorityItems, "Value", "Text", model.PriorityFilter);

        await PopulateCreateFormLookupsAsync(model.CreateForm);
        return model;
    }

    private async Task PopulateCreateFormLookupsAsync(IssueCreateVm vm)
    {
        var assets = await _context.Assets
            .AsNoTracking()
            .OrderBy(a => a.AssetTag)
            .Select(a => new
            {
                a.Id,
                Text = $"{a.AssetTag} - {a.Brand} {a.Model} ({a.SerialNumber})"
            })
            .ToListAsync();

        var staffProfiles = await _context.StaffProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.Id,
                Text = $"{s.FullName} ({s.EmployeeNumber}) - {(s.User != null ? s.User.Email : "No Email")}"
            })
            .ToListAsync();

        vm.AssetLookupItems = assets.Select(a => new LookupOptionVm { Id = a.Id, Text = a.Text }).ToList();
        vm.StaffLookupItems = staffProfiles.Select(s => new LookupOptionVm { Id = s.Id, Text = s.Text }).ToList();
        vm.Assets = new SelectList(assets, "Id", "Text", vm.AssetId);
        vm.StaffProfiles = new SelectList(staffProfiles, "Id", "Text", vm.ReportedForStaffProfileId);

        if (vm.AssetId.HasValue && string.IsNullOrWhiteSpace(vm.AssetSearchText))
        {
            vm.AssetSearchText = vm.AssetLookupItems.FirstOrDefault(x => x.Id == vm.AssetId.Value)?.Text;
        }

        if (vm.ReportedForStaffProfileId.HasValue && string.IsNullOrWhiteSpace(vm.ReportedForSearchText))
        {
            vm.ReportedForSearchText = vm.StaffLookupItems.FirstOrDefault(x => x.Id == vm.ReportedForStaffProfileId.Value)?.Text;
        }
    }

    private async Task<List<IssueListItemVm>> LoadDefaultIssueItemsAsync()
    {
        return await _context.Issues
            .AsNoTracking()
            .Include(i => i.Asset)
            .Include(i => i.ReportedForStaffProfile)
            .Where(i => i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(200)
            .Select(i => new IssueListItemVm
            {
                Id = i.Id,
                Title = i.Title,
                Category = i.Category,
                Description = i.Description,
                Status = i.Status,
                Priority = i.Priority,
                AssetLabel = i.AssetId.HasValue
                    ? $"{i.Asset!.AssetTag} ({i.Asset.SerialNumber})"
                    : "No Asset",
                ReportedForLabel = i.ReportedForStaffProfileId.HasValue
                    ? i.ReportedForStaffProfile!.FullName
                    : "Not specified",
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
               || Request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private async Task PopulateAdminEditDropDownsAsync(int? selectedAssetId = null, string? selectedAssignedUserId = null)
    {
        var assets = await _context.Assets
            .OrderBy(a => a.AssetTag)
            .Select(a => new { a.Id, Name = $"{a.AssetTag} ({a.SerialNumber})" })
            .ToListAsync();

        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, Email = u.Email ?? u.UserName ?? "(no email)" })
            .ToListAsync();

        ViewData["AssetId"] = new SelectList(assets, "Id", "Name", selectedAssetId);
        ViewData["AssignedToUserId"] = new SelectList(users, "Id", "Email", selectedAssignedUserId);
    }
}
