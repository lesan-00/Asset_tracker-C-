using System.Text;
using System.Security.Claims;
using AssetTracker.Data;
using AssetTracker.Helpers;
using AssetTracker.Models;
using AssetTracker.Models.Reports;
using AssetTracker.Models.Reports.Import;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IImportService _importService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ApplicationDbContext context, IImportService importService, ILogger<ReportsController> logger)
    {
        _context = context;
        _importService = importService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? inventorySearch,
        string? inventoryType,
        string? inventoryStatus,
        string? inventoryDepartment,
        string? inventoryLocation,
        string? activeTargetType,
        string? activeSearch,
        DateTime? historyFrom,
        DateTime? historyTo,
        string? historySearch)
    {
        var vm = await BuildIndexVmAsync();
        vm.InventorySearch = inventorySearch?.Trim();
        vm.InventoryType = inventoryType?.Trim();
        vm.InventoryStatus = inventoryStatus?.Trim();
        vm.InventoryDepartment = inventoryDepartment?.Trim();
        vm.InventoryLocation = inventoryLocation?.Trim();
        vm.ActiveTargetType = activeTargetType?.Trim();
        vm.ActiveSearch = activeSearch?.Trim();
        vm.HistoryFrom = historyFrom;
        vm.HistoryTo = historyTo;
        vm.HistorySearch = historySearch?.Trim();
        return View(vm);
    }

    [HttpGet]
    public IActionResult Inventory() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Assignments() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Issues() => RedirectToAction(nameof(Index));

    [HttpGet]
    public async Task<IActionResult> InventoryView(string? search, string? type, string? status, string? department, string? location)
    {
        var rows = await QueryInventoryRowsAsync(search, type, status, department, location);
        ViewData["Title"] = "Full Asset Register";
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> InventoryExcel(string? search, string? type, string? status, string? department, string? location)
    {
        var rows = await QueryInventoryRowsAsync(search, type, status, department, location);
        var data = BuildCsv(
            new[] { "Asset Tag", "Serial", "Brand", "Model", "Type", "Status", "Department", "Location", "Assignee", "Target Type", "Assigned At", "Created At" },
            rows.Select(r => new[]
            {
                r.AssetTag, r.SerialNumber, r.Brand, r.Model, r.Type, r.Status, r.Department, r.Location,
                r.AssigneeLabel, r.TargetType, r.AssignedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-",
                r.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            }));
        return File(data, "text/csv", $"asset-register-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> InventoryPdf(string? search, string? type, string? status, string? department, string? location)
    {
        var rows = await QueryInventoryRowsAsync(search, type, status, department, location);
        var pdf = SimplePdfBuilder.Build(
            "Full Asset Register",
            new[] { "Tag", "Serial", "Type", "Status", "Department", "Location", "Assignee", "Target", "Assigned" },
            rows.Select(r => (IReadOnlyList<string>)new[]
            {
                r.AssetTag, r.SerialNumber, r.Type, r.Status, r.Department, r.Location, r.AssigneeLabel, r.TargetType, r.AssignedAt?.ToString("yyyy-MM-dd") ?? "-"
            }).ToList());
        return File(pdf, "application/pdf", $"asset-register-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ActiveAssignmentsView(string? targetType, string? search)
    {
        var rows = await QueryActiveAssignmentRowsAsync(targetType, search);
        ViewData["Title"] = "Active Assignments";
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> ActiveAssignmentsExcel(string? targetType, string? search)
    {
        var rows = await QueryActiveAssignmentRowsAsync(targetType, search);
        var data = BuildCsv(
            new[] { "Assignment #", "Target Type", "Target", "Asset", "Location", "Assigned At", "Status" },
            rows.Select(r => new[]
            {
                r.AssignmentId.ToString(), r.TargetType, r.TargetDisplay, $"{r.AssetTag} - {r.AssetDisplay}",
                r.Location, r.AssignedAt.ToString("yyyy-MM-dd HH:mm"), r.Status
            }));
        return File(data, "text/csv", $"active-assignments-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ActiveAssignmentsPdf(string? targetType, string? search)
    {
        var rows = await QueryActiveAssignmentRowsAsync(targetType, search);
        var pdf = SimplePdfBuilder.Build(
            "Active Assignments",
            new[] { "Id", "Target Type", "Target", "Asset", "Location", "Status" },
            rows.Select(r => (IReadOnlyList<string>)new[]
            {
                r.AssignmentId.ToString(), r.TargetType, r.TargetDisplay, r.AssetTag, r.Location, r.Status
            }).ToList());
        return File(pdf, "application/pdf", $"active-assignments-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> AssignmentHistoryView(DateTime? from, DateTime? to, string? search)
    {
        var rows = await QueryAssignmentHistoryRowsAsync(from, to, search);
        ViewData["Title"] = "Assignment History";
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> AssignmentHistoryExcel(DateTime? from, DateTime? to, string? search)
    {
        var rows = await QueryAssignmentHistoryRowsAsync(from, to, search);
        var data = BuildCsv(
            new[] { "Assignment #", "Asset", "Staff", "Department", "Status", "Assigned At", "Returned At" },
            rows.Select(r => new[]
            {
                r.AssignmentId.ToString(), r.AssetTag, r.StaffName, r.Department, r.Status,
                r.AssignedAt.ToString("yyyy-MM-dd HH:mm"), r.ReturnedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"
            }));
        return File(data, "text/csv", $"assignment-history-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> AssignmentHistoryPdf(DateTime? from, DateTime? to, string? search)
    {
        var rows = await QueryAssignmentHistoryRowsAsync(from, to, search);
        var pdf = SimplePdfBuilder.Build(
            "Assignment History",
            new[] { "Id", "Asset", "Staff", "Department", "Status", "Assigned", "Returned" },
            rows.Select(r => (IReadOnlyList<string>)new[]
            {
                r.AssignmentId.ToString(),
                r.AssetTag,
                r.StaffName,
                r.Department,
                r.Status,
                r.AssignedAt.ToString("yyyy-MM-dd"),
                r.ReturnedAt?.ToString("yyyy-MM-dd") ?? "-"
            }).ToList());
        return File(pdf, "application/pdf", $"assignment-history-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
    }

    [HttpGet]
    public IActionResult ImportAssets()
    {
        return View(new AssetImportPageVm
        {
            Upload = new ImportUploadVm
            {
                UpdateExisting = true,
                SkipInvalid = true
            }
        });
    }

    [HttpGet]
    public IActionResult ImportStaff()
    {
        return View(new StaffImportPageVm
        {
            Upload = new ImportUploadVm
            {
                UpdateExisting = true,
                SkipInvalid = true
            }
        });
    }

    [HttpGet("Reports/ImportAssets/Template")]
    public IActionResult ImportAssetsTemplate()
    {
        var bytes = _importService.BuildAssetsTemplateCsv();
        return File(bytes, "text/csv", "assets-import-template.csv");
    }

    [HttpGet("Reports/ImportStaff/Template")]
    public IActionResult ImportStaffTemplate()
    {
        var bytes = _importService.BuildStaffTemplateCsv();
        return File(bytes, "text/csv", "staff-import-template.csv");
    }

    [HttpPost("Reports/ImportAssets/Preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportAssetsPreview(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new AssetImportPageVm { Upload = upload };
        if (upload.File is null)
        {
            vm.Message = "Please choose a file to preview.";
            return View("ImportAssets", vm);
        }

        try
        {
            vm.Preview = await _importService.ParseAssetsAsync(upload.File, cancellationToken);
            vm.Upload.ImportKey = vm.Preview.ImportKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Asset import preview failed.");
            vm.Message = ex.Message;
        }

        return View("ImportAssets", vm);
    }

    [HttpPost("Reports/ImportAssets/Commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportAssetsCommit(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new AssetImportPageVm { Upload = upload };
        if (string.IsNullOrWhiteSpace(upload.ImportKey))
        {
            vm.Message = "Preview expired or missing. Please preview again.";
            return View("ImportAssets", vm);
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Challenge();
        }

        try
        {
            vm.CommitResult = await _importService.CommitAssetsAsync(
                upload.ImportKey,
                upload.UpdateExisting,
                upload.SkipInvalid,
                actorUserId,
                cancellationToken);

            vm.Message = "Assets import completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Asset import commit failed.");
            vm.Message = ex.Message;
        }

        return View("ImportAssets", vm);
    }

    [HttpPost("Reports/ImportStaff/Preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStaffPreview(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new StaffImportPageVm { Upload = upload };
        if (upload.File is null)
        {
            vm.Message = "Please choose a file to preview.";
            return View("ImportStaff", vm);
        }

        try
        {
            vm.Preview = await _importService.ParseStaffAsync(upload.File, cancellationToken);
            vm.Upload.ImportKey = vm.Preview.ImportKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff import preview failed.");
            vm.Message = ex.Message;
        }

        return View("ImportStaff", vm);
    }

    [HttpPost("Reports/ImportStaff/Commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStaffCommit(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new StaffImportPageVm { Upload = upload };
        if (string.IsNullOrWhiteSpace(upload.ImportKey))
        {
            vm.Message = "Preview expired or missing. Please preview again.";
            return View("ImportStaff", vm);
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Challenge();
        }

        try
        {
            vm.CommitResult = await _importService.CommitStaffAsync(
                upload.ImportKey,
                upload.UpdateExisting,
                upload.SkipInvalid,
                actorUserId,
                cancellationToken);
            vm.Message = "Staff import completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Staff import commit failed.");
            vm.Message = ex.Message;
        }

        return View("ImportStaff", vm);
    }

    [HttpGet("Reports/ImportErrors/{errorReportKey}")]
    public IActionResult ImportErrorReport(string errorReportKey)
    {
        if (!_importService.TryGetErrorReport(errorReportKey, out var bytes))
        {
            return NotFound();
        }

        return File(bytes, "text/csv", $"import-errors-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private async Task<ReportsIndexVm> BuildIndexVmAsync()
    {
        var types = Enum.GetValues<AssetType>()
            .Select(t => new SelectListItem { Value = t.ToString(), Text = t.ToString() })
            .ToList();
        types.Insert(0, new SelectListItem { Value = "all", Text = "All Types" });

        var statuses = Enum.GetValues<AssetStatus>()
            .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() })
            .ToList();
        statuses.Insert(0, new SelectListItem { Value = "all", Text = "All Statuses" });

        var targets = Enum.GetValues<AssignmentTargetTypeFilter>()
            .Select(t => new SelectListItem { Value = t.ToString(), Text = t.ToString().ToUpperInvariant() })
            .ToList();

        return await Task.FromResult(new ReportsIndexVm
        {
            TypeOptions = new SelectList(types, "Value", "Text"),
            StatusOptions = new SelectList(statuses, "Value", "Text"),
            TargetTypeOptions = new SelectList(targets, "Value", "Text")
        });
    }

    private async Task<List<AssetRegisterRowVm>> QueryInventoryRowsAsync(
        string? search,
        string? type,
        string? status,
        string? department,
        string? location)
    {
        var activeAssignments = _context.Assignments
            .AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Active
                        || a.Status == AssignmentStatus.PendingAcceptance
                        || a.Status == AssignmentStatus.Accepted
                        || a.Status == AssignmentStatus.ReturnRequested);

        // EF Core + Pomelo may fail translating grouped subqueries that project navigations.
        // We compute latest active assignment per asset via aggregate subqueries and rejoin.
        var latestActiveAt = activeAssignments
            .GroupBy(a => a.AssetId)
            .Select(g => new
            {
                AssetId = g.Key,
                MaxAssignedAt = g.Max(x => x.AssignedAt)
            });

        var latestActiveRows = from a in activeAssignments
                               join t in latestActiveAt
                                   on new { a.AssetId, a.AssignedAt } equals new { t.AssetId, AssignedAt = t.MaxAssignedAt }
                               select new
                               {
                                   a.AssetId,
                                   a.Id
                               };

        var latestActiveId = latestActiveRows
            .GroupBy(x => x.AssetId)
            .Select(g => new
            {
                AssetId = g.Key,
                MaxId = g.Max(x => x.Id)
            });

        var latestActiveByAsset = from lid in latestActiveId
                                  join a in _context.Assignments.AsNoTracking() on lid.MaxId equals a.Id
                                  join s in _context.StaffProfiles.AsNoTracking() on a.StaffProfileId equals s.Id into staffJoin
                                  from s in staffJoin.DefaultIfEmpty()
                                  select new
                                  {
                                      a.AssetId,
                                      a.AssignedAt,
                                      StaffName = s != null ? s.FullName : null,
                                      Department = s != null ? s.Department : null
                                  };

        var assetsQuery = _context.Assets.AsNoTracking();
        var normalizedSearch = search?.Trim();
        var normalizedDepartment = department?.Trim();
        var normalizedLocation = location?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var lowered = normalizedSearch.ToLower();
            assetsQuery = assetsQuery.Where(a =>
                a.AssetTag.ToLower().Contains(lowered) ||
                a.SerialNumber.ToLower().Contains(lowered) ||
                a.Brand.ToLower().Contains(lowered) ||
                a.Model.ToLower().Contains(lowered));
        }

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<AssetType>(type, true, out var parsedType))
        {
            assetsQuery = assetsQuery.Where(a => a.AssetType == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<AssetStatus>(status, true, out var parsedStatus))
        {
            assetsQuery = assetsQuery.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(normalizedLocation))
        {
            var lowered = normalizedLocation.ToLower();
            assetsQuery = assetsQuery.Where(a => a.Location.ToLower().Contains(lowered));
        }

        var query = from asset in assetsQuery
                    join active in latestActiveByAsset on asset.Id equals active.AssetId into activeJoin
                    from active in activeJoin.DefaultIfEmpty()
                    select new AssetRegisterRowVm
                    {
                        AssetId = asset.Id,
                        AssetTag = asset.AssetTag,
                        SerialNumber = asset.SerialNumber,
                        Brand = asset.Brand,
                        Model = asset.Model,
                        Type = asset.AssetType.ToString(),
                        Status = asset.Status.ToString(),
                        Department = active != null && active.Department != null ? active.Department : "-",
                        Location = asset.Location,
                        AssigneeLabel = active != null && active.StaffName != null ? active.StaffName : "-",
                        TargetType = active != null ? "STAFF" : "-",
                        AssignedAt = active != null ? active.AssignedAt : null,
                        CreatedAt = asset.CreatedAt
                    };

        if (!string.IsNullOrWhiteSpace(normalizedDepartment))
        {
            var lowered = normalizedDepartment.ToLower();
            query = query.Where(r => r.Department.ToLower().Contains(lowered));
        }

        return await query
            .OrderBy(r => r.AssetTag)
            .ToListAsync();
    }

    private async Task<List<ActiveAssignmentRowVm>> QueryActiveAssignmentRowsAsync(string? targetType, string? search)
    {
        var effectiveTarget = ParseTargetType(targetType);
        var query = _context.Assignments
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.StaffProfile)
            .Where(a => a.Status == AssignmentStatus.Active
                        || a.Status == AssignmentStatus.PendingAcceptance
                        || a.Status == AssignmentStatus.Accepted
                        || a.Status == AssignmentStatus.ReturnRequested)
            .AsQueryable();

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var lowered = normalizedSearch.ToLower();
            query = effectiveTarget switch
            {
                AssignmentTargetTypeFilter.Location => query.Where(a =>
                    a.Asset.Location.ToLower().Contains(lowered) ||
                    a.Asset.AssetTag.ToLower().Contains(lowered)),
                AssignmentTargetTypeFilter.Department => query.Where(a =>
                    a.StaffProfile.Department.ToLower().Contains(lowered) ||
                    a.Asset.AssetTag.ToLower().Contains(lowered)),
                _ => query.Where(a =>
                    a.StaffProfile.FullName.ToLower().Contains(lowered) ||
                    a.Asset.AssetTag.ToLower().Contains(lowered) ||
                    a.Asset.SerialNumber.ToLower().Contains(lowered))
            };
        }

        return await query
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new ActiveAssignmentRowVm
            {
                AssignmentId = a.Id,
                TargetType = effectiveTarget.ToString().ToUpperInvariant(),
                TargetDisplay = effectiveTarget == AssignmentTargetTypeFilter.Location
                    ? a.Asset.Location
                    : (effectiveTarget == AssignmentTargetTypeFilter.Department
                        ? a.StaffProfile.Department
                        : a.StaffProfile.FullName),
                AssetTag = a.Asset.AssetTag,
                AssetDisplay = $"{a.Asset.Brand} {a.Asset.Model} ({a.Asset.SerialNumber})",
                Location = a.Asset.Location,
                AssignedAt = a.AssignedAt,
                Status = NormalizeAssignmentStatus(a.Status).ToString()
            })
            .ToListAsync();
    }

    private async Task<List<AssignmentHistoryRowVm>> QueryAssignmentHistoryRowsAsync(DateTime? from, DateTime? to, string? search)
    {
        var query = _context.Assignments
            .AsNoTracking()
            .Include(a => a.Asset)
            .Include(a => a.StaffProfile)
            .AsQueryable();

        if (from.HasValue)
        {
            var start = from.Value.Date;
            query = query.Where(a => a.AssignedAt >= start);
        }

        if (to.HasValue)
        {
            var endExclusive = to.Value.Date.AddDays(1);
            query = query.Where(a => a.AssignedAt < endExclusive);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var lowered = normalizedSearch.ToLower();
            query = query.Where(a =>
                a.Id.ToString().Contains(lowered) ||
                a.Asset.AssetTag.ToLower().Contains(lowered) ||
                a.StaffProfile.FullName.ToLower().Contains(lowered));
        }

        return await query
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new AssignmentHistoryRowVm
            {
                AssignmentId = a.Id,
                AssetTag = a.Asset.AssetTag,
                StaffName = a.StaffProfile.FullName,
                Department = a.StaffProfile.Department,
                Status = NormalizeAssignmentStatus(a.Status).ToString(),
                AssignedAt = a.AssignedAt,
                ReturnedAt = a.ReturnedApprovedAt
            })
            .ToListAsync();
    }

    private static AssignmentTargetTypeFilter ParseTargetType(string? targetType)
    {
        if (!string.IsNullOrWhiteSpace(targetType) &&
            Enum.TryParse<AssignmentTargetTypeFilter>(targetType, true, out var parsed))
        {
            return parsed;
        }

        return AssignmentTargetTypeFilter.Staff;
    }

    private static AssignmentStatus NormalizeAssignmentStatus(AssignmentStatus status)
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

    private static byte[] BuildCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        static string Escape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }

            return value;
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
