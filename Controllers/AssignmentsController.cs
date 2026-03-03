using AssetTracker.Data;
using AssetTracker.Models.Assignments;
using AssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class AssignmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(ApplicationDbContext context, ILogger<AssignmentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var normalizedQuery = q?.Trim();
        var query = _context.Assignments
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.StaffProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var lowered = normalizedQuery.ToLower();
            query = query.Where(a =>
                a.Asset.AssetTag.ToLower().Contains(lowered) ||
                a.Asset.SerialNumber.ToLower().Contains(lowered) ||
                a.Asset.Brand.ToLower().Contains(lowered) ||
                a.Asset.Model.ToLower().Contains(lowered) ||
                a.StaffProfile.FullName.ToLower().Contains(lowered) ||
                (a.Notes != null && a.Notes.ToLower().Contains(lowered)));
        }

        var cards = await query
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Take(200)
            .Select(a => new AssignmentCardVm
            {
                AssignmentId = a.Id,
                AssetId = a.AssetId,
                AssetName = $"{a.Asset.Brand} {a.Asset.Model}",
                AssetTag = a.Asset.AssetTag,
                SerialNumber = a.Asset.SerialNumber,
                AssetType = ToUpperSnakeCase(a.Asset.AssetType.ToString()),
                StaffProfileId = a.StaffProfileId,
                StaffFullName = BuildReceiverLabel(a.StaffProfile.FullName, a.Notes),
                IssuedDate = a.AssignedAt,
                Status = a.Status
            })
            .ToListAsync();

        var vm = new AssignmentIndexVm
        {
            Query = normalizedQuery,
            Assignments = cards
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var assignment = await _context.Assignments
            .Include(a => a.Asset)
            .Include(a => a.StaffProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is null)
        {
            return NotFound();
        }

        return View(assignment);
    }

    public async Task<IActionResult> Create()
    {
        var vm = await BuildCreateVmAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewAssignmentVm vm)
    {
        if (vm.TargetType == AssignmentTargetType.Department &&
            string.IsNullOrWhiteSpace(vm.Department) &&
            !string.IsNullOrWhiteSpace(vm.ReceiverSearchText))
        {
            vm.Department = vm.ReceiverSearchText.Trim();
            ModelState.Remove(nameof(NewAssignmentVm.Department));
        }

        if (vm.TargetType == AssignmentTargetType.Location &&
            string.IsNullOrWhiteSpace(vm.Location) &&
            !string.IsNullOrWhiteSpace(vm.ReceiverSearchText))
        {
            vm.Location = vm.ReceiverSearchText.Trim();
            ModelState.Remove(nameof(NewAssignmentVm.Location));
        }

        ModelState.Clear();
        TryValidateModel(vm);

        var asset = vm.AssetId.HasValue
            ? await _context.Assets.FirstOrDefaultAsync(a => a.Id == vm.AssetId.Value)
            : null;

        if (asset is null)
        {
            ModelState.AddModelError(nameof(NewAssignmentVm.AssetId), "Selected asset does not exist.");
        }
        else if (asset.Status != AssetStatus.InStock)
        {
            ModelState.AddModelError(nameof(NewAssignmentVm.AssetId), "Only InStock assets can be assigned.");
        }

        var hasActiveAssignment = vm.AssetId.HasValue &&
                                  await _context.Assignments
                                      .AsNoTracking()
                                      .AnyAsync(a => a.AssetId == vm.AssetId.Value && IsActiveStatus(a.Status));
        if (hasActiveAssignment)
        {
            ModelState.AddModelError(nameof(NewAssignmentVm.AssetId), "This asset already has an active assignment.");
        }

        int? resolvedStaffProfileId = null;
        if (vm.TargetType == AssignmentTargetType.Staff)
        {
            if (!vm.StaffProfileId.HasValue || !await _context.StaffProfiles.AnyAsync(s => s.Id == vm.StaffProfileId.Value))
            {
                ModelState.AddModelError(nameof(NewAssignmentVm.StaffProfileId), "Selected staff profile does not exist.");
            }
            else
            {
                resolvedStaffProfileId = vm.StaffProfileId.Value;
            }
        }
        else
        {
            resolvedStaffProfileId = await ResolveFallbackStaffProfileIdAsync(vm);
            if (!resolvedStaffProfileId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "At least one staff profile is required to save a non-staff assignment target.");
            }
        }

        if (!ModelState.IsValid)
        {
            vm = await BuildCreateVmAsync(vm);
            return View(vm);
        }

        var notesParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(vm.IssueCondition))
        {
            notesParts.Add($"Issue Condition: {vm.IssueCondition.Trim()}");
        }

        if (vm.IssueDate.HasValue)
        {
            notesParts.Add($"Issue Date: {vm.IssueDate.Value:yyyy-MM-dd}");
        }

        if (vm.TargetType == AssignmentTargetType.Department && !string.IsNullOrWhiteSpace(vm.Department))
        {
            notesParts.Add($"TARGET:DEPARTMENT={vm.Department.Trim()}");
        }
        else if (vm.TargetType == AssignmentTargetType.Location && !string.IsNullOrWhiteSpace(vm.Location))
        {
            notesParts.Add($"TARGET:LOCATION={vm.Location.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(vm.Notes))
        {
            notesParts.Add(vm.Notes.Trim());
        }

        var finalNotes = string.Join(Environment.NewLine, notesParts);
        if (finalNotes.Length > 500)
        {
            finalNotes = finalNotes[..500];
        }

        var assignment = new Assignment
        {
            AssetId = vm.AssetId!.Value,
            StaffProfileId = resolvedStaffProfileId!.Value,
            Notes = string.IsNullOrWhiteSpace(finalNotes) ? null : finalNotes
        };

        assignment.Status = AssignmentStatus.Active;
        var assignedAt = vm.IssueDate?.Date ?? DateTime.UtcNow;
        assignment.AssignedAt = assignedAt;
        assignment.AcceptedAt = assignedAt;
        assignment.ReturnRequestedAt = null;
        assignment.ReturnedApprovedAt = null;

        asset!.Status = AssetStatus.Assigned;

        try
        {
            _context.Assignments.Add(assignment);
            await _context.SaveChangesAsync();
            TempData["AssignmentSuccess"] = "Assignment created successfully.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Assignment create failed for asset {AssetId}.", vm.AssetId);
            ModelState.AddModelError(string.Empty, "Unable to save assignment right now. Please try again.");
            vm = await BuildCreateVmAsync(vm);
            return View(vm);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReturned(int id)
    {
        var assignment = await _context.Assignments
            .Include(a => a.Asset)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is null)
        {
            return NotFound();
        }

        var normalizedStatus = NormalizeStatus(assignment.Status);
        if (normalizedStatus != AssignmentStatus.Active)
        {
            return BadRequest("Only active assignments can be marked as returned.");
        }

        assignment.Status = AssignmentStatus.Returned;
        assignment.ReturnedApprovedAt = DateTime.UtcNow;
        assignment.Asset.Status = AssetStatus.InStock;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revert(int id)
    {
        var assignment = await _context.Assignments
            .Include(a => a.Asset)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is null)
        {
            return NotFound();
        }

        var normalizedStatus = NormalizeStatus(assignment.Status);
        if (normalizedStatus != AssignmentStatus.Active)
        {
            return BadRequest("Only active assignments can be reverted.");
        }

        assignment.Status = AssignmentStatus.Reverted;
        assignment.ReturnedApprovedAt = DateTime.UtcNow;
        assignment.Asset.Status = AssetStatus.InStock;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var assignment = await _context.Assignments
            .Include(a => a.Asset)
            .Include(a => a.StaffProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is null)
        {
            return NotFound();
        }

        return View(assignment);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var assignment = await _context.Assignments
            .Include(a => a.Asset)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment is not null)
        {
            if (assignment.Status is AssignmentStatus.Active
                or AssignmentStatus.PendingAcceptance
                or AssignmentStatus.Accepted
                or AssignmentStatus.ReturnRequested)
            {
                assignment.Asset.Status = AssetStatus.InStock;
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<NewAssignmentVm> BuildCreateVmAsync(NewAssignmentVm? source = null)
    {
        var vm = source ?? new NewAssignmentVm();
        if (!vm.IssueDate.HasValue)
        {
            vm.IssueDate = DateTime.UtcNow.Date;
        }

        if (vm.AssetId.HasValue && string.IsNullOrWhiteSpace(vm.AssetSearchText))
        {
            vm.AssetSearchText = await _context.Assets
                .AsNoTracking()
                .Where(a => a.Id == vm.AssetId.Value)
                .Select(a => $"{a.AssetTag} - {a.Brand} {a.Model} ({a.SerialNumber})")
                .FirstOrDefaultAsync();
        }

        if (vm.TargetType == AssignmentTargetType.Staff &&
            vm.StaffProfileId.HasValue &&
            string.IsNullOrWhiteSpace(vm.ReceiverSearchText))
        {
            vm.ReceiverSearchText = await _context.StaffProfiles
                .AsNoTracking()
                .Where(s => s.Id == vm.StaffProfileId.Value)
                .Select(s => $"{s.FullName} ({s.EmployeeNumber})")
                .FirstOrDefaultAsync();
        }

        if (vm.TargetType == AssignmentTargetType.Department &&
            string.IsNullOrWhiteSpace(vm.ReceiverSearchText))
        {
            vm.ReceiverSearchText = vm.Department;
        }

        if (vm.TargetType == AssignmentTargetType.Location &&
            string.IsNullOrWhiteSpace(vm.ReceiverSearchText))
        {
            vm.ReceiverSearchText = vm.Location;
        }

        return vm;
    }

    private async Task<int?> ResolveFallbackStaffProfileIdAsync(NewAssignmentVm vm)
    {
        var staffQuery = _context.StaffProfiles.AsNoTracking().AsQueryable();

        if (vm.TargetType == AssignmentTargetType.Department && !string.IsNullOrWhiteSpace(vm.Department))
        {
            var dept = vm.Department.Trim().ToLower();
            var deptMatch = await staffQuery
                .Where(s => s.Department.ToLower() == dept)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
            if (deptMatch.HasValue)
            {
                return deptMatch.Value;
            }
        }

        return await staffQuery
            .OrderBy(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();
    }

    private static string ToUpperSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static AssignmentStatus NormalizeStatus(AssignmentStatus status)
    {
        return status switch
        {
            AssignmentStatus.PendingAcceptance => AssignmentStatus.Active,
            AssignmentStatus.Accepted => AssignmentStatus.Active,
            AssignmentStatus.ReturnRequested => AssignmentStatus.Active,
            AssignmentStatus.ReturnedApproved => AssignmentStatus.Returned,
            _ => status
        };
    }

    private static bool IsActiveStatus(AssignmentStatus status)
    {
        var normalized = NormalizeStatus(status);
        return normalized == AssignmentStatus.Active;
    }

    private static string BuildReceiverLabel(string staffName, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return staffName;
        }

        var lines = notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("TARGET:DEPARTMENT=", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["TARGET:DEPARTMENT=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return $"Dept: {value}";
                }
            }

            if (line.StartsWith("TARGET:LOCATION=", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["TARGET:LOCATION=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return $"Loc: {value}";
                }
            }
        }

        return staffName;
    }
}
