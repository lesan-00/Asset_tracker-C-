using System.Text;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using AssetTracker.Data;
using AssetTracker.Helpers;
using AssetTracker.Models;
using AssetTracker.Models.Reports;
using AssetTracker.Models.Reports.Import;
using AssetTracker.Models.Vendors;
using AssetTracker.Services;
using ClosedXML.Excel;
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
        string? historySearch,
        ProcurementReportType procurementReportType = ProcurementReportType.PrSummary,
        DateTime? procurementFrom = null,
        DateTime? procurementTo = null,
        int? procurementVendorId = null,
        string? procurementDepartment = null,
        string? procurementSearch = null)
    {
        var vm = await BuildIndexVmAsync(procurementReportType, procurementVendorId);
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
        vm.ProcurementReportType = procurementReportType;
        vm.ProcurementFrom = procurementFrom;
        vm.ProcurementTo = procurementTo;
        vm.ProcurementVendorId = procurementVendorId;
        vm.ProcurementDepartment = procurementDepartment?.Trim();
        vm.ProcurementSearch = procurementSearch?.Trim();
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
    public async Task<IActionResult> ProcurementView(
        ProcurementReportType reportType = ProcurementReportType.PrSummary,
        DateTime? from = null,
        DateTime? to = null,
        int? vendorId = null,
        string? department = null,
        string? search = null)
    {
        var report = await BuildProcurementReportAsync(reportType, from, to, vendorId, department, search);
        ViewData["Title"] = report.Title;
        return View("ProcurementReportView", report);
    }

    [HttpGet]
    public async Task<IActionResult> ProcurementExcel(
        ProcurementReportType reportType = ProcurementReportType.PrSummary,
        DateTime? from = null,
        DateTime? to = null,
        int? vendorId = null,
        string? department = null,
        string? search = null)
    {
        var report = await BuildProcurementReportAsync(reportType, from, to, vendorId, department, search);
        var workbookBytes = BuildExcelWorkbook(report.Title, report.Headers, report.Rows);
        return File(workbookBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{GetReportFileNamePrefix(reportType)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ProcurementPdf(
        ProcurementReportType reportType = ProcurementReportType.PrSummary,
        DateTime? from = null,
        DateTime? to = null,
        int? vendorId = null,
        string? department = null,
        string? search = null)
    {
        var report = await BuildProcurementReportAsync(reportType, from, to, vendorId, department, search);
        var pdf = SimplePdfBuilder.Build(report.Title, report.Headers, report.Rows);
        return File(pdf, "application/pdf", $"{GetReportFileNamePrefix(reportType)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
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
            return RedirectToAction(nameof(ImportAssets));
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

        return RedirectToAction(nameof(ImportAssets));
    }

    [HttpPost("Reports/ImportAssets/Commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportAssetsCommit(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new AssetImportPageVm { Upload = upload };
        if (string.IsNullOrWhiteSpace(upload.ImportKey))
        {
            vm.Message = "Preview expired or missing. Please preview again.";
            return RedirectToAction(nameof(ImportAssets));
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

        return RedirectToAction(nameof(ImportAssets));
    }

    [HttpPost("Reports/ImportStaff/Preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStaffPreview(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new StaffImportPageVm { Upload = upload };
        if (upload.File is null)
        {
            vm.Message = "Please choose a file to preview.";
            return RedirectToAction(nameof(ImportStaff));
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

        return RedirectToAction(nameof(ImportStaff));
    }

    [HttpPost("Reports/ImportStaff/Commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStaffCommit(ImportUploadVm upload, CancellationToken cancellationToken)
    {
        var vm = new StaffImportPageVm { Upload = upload };
        if (string.IsNullOrWhiteSpace(upload.ImportKey))
        {
            vm.Message = "Preview expired or missing. Please preview again.";
            return RedirectToAction(nameof(ImportStaff));
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

        return RedirectToAction(nameof(ImportStaff));
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

    private async Task<ReportsIndexVm> BuildIndexVmAsync(
        ProcurementReportType selectedProcurementReportType = ProcurementReportType.PrSummary,
        int? selectedVendorId = null)
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

        var procurementReports = Enum.GetValues<ProcurementReportType>()
            .Select(r => new SelectListItem { Value = r.ToString(), Text = GetEnumDisplayName(r) })
            .ToList();

        var vendors = await _context.Vendors
            .AsNoTracking()
            .Where(v => !v.IsArchived)
            .OrderBy(v => v.CompanyName)
            .Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = $"{v.CompanyName} ({v.VendorCode})"
            })
            .ToListAsync();
        vendors.Insert(0, new SelectListItem { Value = string.Empty, Text = "All Vendors" });

        return await Task.FromResult(new ReportsIndexVm
        {
            TypeOptions = new SelectList(types, "Value", "Text"),
            StatusOptions = new SelectList(statuses, "Value", "Text"),
            TargetTypeOptions = new SelectList(targets, "Value", "Text"),
            ProcurementReportType = selectedProcurementReportType,
            ProcurementReportOptions = new SelectList(procurementReports, "Value", "Text", selectedProcurementReportType.ToString()),
            ProcurementVendorId = selectedVendorId,
            ProcurementVendorOptions = new SelectList(vendors, "Value", "Text", selectedVendorId?.ToString())
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

    private async Task<ProcurementReportVm> BuildProcurementReportAsync(
        ProcurementReportType reportType,
        DateTime? from,
        DateTime? to,
        int? vendorId,
        string? department,
        string? search)
    {
        var fromDate = from?.Date;
        var toExclusive = to?.Date.AddDays(1);
        var normalizedDepartment = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var vendorLabel = "All Vendors";

        if (vendorId.HasValue)
        {
            vendorLabel = await _context.Vendors
                .AsNoTracking()
                .Where(v => v.Id == vendorId.Value)
                .Select(v => v.CompanyName)
                .FirstOrDefaultAsync() ?? "Selected Vendor";
        }

        return reportType switch
        {
            ProcurementReportType.PrSummary => await BuildPrSummaryReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.PendingApprovalPrs => await BuildPendingApprovalPrsReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.PrToPoConversion => await BuildPrToPoConversionReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.DepartmentPrSpending => await BuildDepartmentPrSpendingReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.PoSummary => await BuildPoSummaryReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.VendorPoSpend => await BuildVendorPoSpendReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.OpenPurchaseOrders => await BuildOpenPurchaseOrdersReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.VendorDeliveryPerformance => await BuildVendorDeliveryPerformanceReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.InvoiceSummary => await BuildInvoiceSummaryReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.PendingOverdueInvoices => await BuildPendingOverdueInvoicesReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.VendorInvoiceSpend => await BuildVendorInvoiceSpendReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            ProcurementReportType.InvoiceVsPoMatching => await BuildInvoiceVsPoMatchingReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel),
            _ => await BuildPrSummaryReportAsync(reportType, fromDate, toExclusive, vendorId, normalizedDepartment, normalizedSearch, vendorLabel)
        };
    }

    private async Task<ProcurementReportVm> BuildPrSummaryReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.RequestedByStaff)
            .Include(pr => pr.Lines)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(pr => pr.RequestDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(pr => pr.RequestDate < toExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(pr => pr.Department != null && pr.Department.ToLower().Contains(loweredDepartment));
        }

        if (vendorId.HasValue)
        {
            query = query.Where(pr => pr.Lines.Any(l => l.SuggestedVendorId == vendorId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(pr =>
                pr.PrNumber.ToLower().Contains(lowered) ||
                (pr.Department != null && pr.Department.ToLower().Contains(lowered)) ||
                (pr.RequestedByStaff != null && pr.RequestedByStaff.FullName.ToLower().Contains(lowered)));
        }

        var rowsData = await query
            .OrderByDescending(pr => pr.RequestDate)
            .ThenByDescending(pr => pr.Id)
            .Select(pr => new
            {
                pr.PrNumber,
                pr.RequestDate,
                RequestedBy = pr.RequestedByStaff != null ? pr.RequestedByStaff.FullName : "-",
                Department = pr.Department ?? "-",
                Priority = pr.Priority.ToString(),
                Status = pr.Status.ToString(),
                pr.NeededByDate,
                EstimatedTotal = pr.Lines.Sum(l => (decimal?)l.EstimatedTotal) ?? 0m
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.PrNumber,
            r.RequestDate.ToString("yyyy-MM-dd"),
            r.RequestedBy,
            r.Department,
            r.Priority,
            r.Status,
            r.NeededByDate?.ToString("yyyy-MM-dd") ?? "-",
            $"KES {r.EstimatedTotal:N2}"
        }).ToList();

        var totalEstimated = rowsData.Sum(r => r.EstimatedTotal);
        return CreateProcurementReportVm(
            reportType,
            "PR Summary",
            "Summary of purchase requests by requester, department, priority, and status.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["PR Number", "Request Date", "Requested By", "Department", "Priority", "Status", "Needed By", "Estimated Total"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Total PRs", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Approved PRs", Value = rowsData.Count(r => r.Status == PurchaseRequestStatus.Approved.ToString()).ToString() },
                new ProcurementSummaryMetricVm { Label = "Converted to PO", Value = rowsData.Count(r => r.Status == PurchaseRequestStatus.ConvertedToPO.ToString()).ToString() },
                new ProcurementSummaryMetricVm { Label = "Estimated Total", Value = $"KES {totalEstimated:N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildPendingApprovalPrsReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.RequestedByStaff)
            .Include(pr => pr.Lines)
            .Where(pr => pr.Status == PurchaseRequestStatus.Submitted || pr.Status == PurchaseRequestStatus.PendingApproval)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(pr => pr.RequestDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(pr => pr.RequestDate < toExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(pr => pr.Department != null && pr.Department.ToLower().Contains(loweredDepartment));
        }

        if (vendorId.HasValue)
        {
            query = query.Where(pr => pr.Lines.Any(l => l.SuggestedVendorId == vendorId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(pr =>
                pr.PrNumber.ToLower().Contains(lowered) ||
                (pr.RequestedByStaff != null && pr.RequestedByStaff.FullName.ToLower().Contains(lowered)) ||
                (pr.Department != null && pr.Department.ToLower().Contains(lowered)));
        }

        var now = DateTime.UtcNow.Date;
        var rowsData = await query
            .OrderBy(pr => pr.NeededByDate)
            .ThenBy(pr => pr.RequestDate)
            .Select(pr => new
            {
                pr.PrNumber,
                pr.RequestDate,
                RequestedBy = pr.RequestedByStaff != null ? pr.RequestedByStaff.FullName : "-",
                Department = pr.Department ?? "-",
                Priority = pr.Priority.ToString(),
                NeededByDate = pr.NeededByDate,
                EstimatedTotal = pr.Lines.Sum(l => (decimal?)l.EstimatedTotal) ?? 0m
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.PrNumber,
            r.RequestDate.ToString("yyyy-MM-dd"),
            r.RequestedBy,
            r.Department,
            r.Priority,
            r.NeededByDate?.ToString("yyyy-MM-dd") ?? "-",
            (now - r.RequestDate.Date).Days.ToString(),
            $"KES {r.EstimatedTotal:N2}"
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Pending Approval PRs",
            "Purchase requests awaiting procurement approval.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["PR Number", "Request Date", "Requested By", "Department", "Priority", "Needed By", "Age (Days)", "Estimated Total"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Pending PRs", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Urgent/High", Value = rowsData.Count(r => r.Priority is "Urgent" or "High").ToString() },
                new ProcurementSummaryMetricVm { Label = "Estimated Total", Value = $"KES {rowsData.Sum(r => r.EstimatedTotal):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildPrToPoConversionReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.Lines)
            .Include(pr => pr.PurchaseOrders)
                .ThenInclude(po => po.Vendor)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(pr => pr.RequestDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(pr => pr.RequestDate < toExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(pr => pr.Department != null && pr.Department.ToLower().Contains(loweredDepartment));
        }

        if (vendorId.HasValue)
        {
            query = query.Where(pr => pr.Lines.Any(l => l.SuggestedVendorId == vendorId.Value) ||
                                      pr.PurchaseOrders.Any(po => po.VendorId == vendorId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(pr =>
                pr.PrNumber.ToLower().Contains(lowered) ||
                pr.PurchaseOrders.Any(po => po.PoNumber.ToLower().Contains(lowered)) ||
                (pr.Department != null && pr.Department.ToLower().Contains(lowered)));
        }

        var rowsData = await query
            .OrderByDescending(pr => pr.RequestDate)
            .Select(pr => new
            {
                pr.PrNumber,
                PrStatus = pr.Status.ToString(),
                pr.RequestDate,
                EstimatedTotal = pr.Lines.Sum(l => (decimal?)l.EstimatedTotal) ?? 0m,
                LinkedPo = pr.PurchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Select(po => new { po.PoNumber, po.OrderDate, Vendor = po.Vendor.CompanyName })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.PrNumber,
            r.PrStatus,
            r.RequestDate.ToString("yyyy-MM-dd"),
            $"KES {r.EstimatedTotal:N2}",
            r.LinkedPo?.PoNumber ?? "-",
            r.LinkedPo?.OrderDate.ToString("yyyy-MM-dd") ?? "-",
            r.LinkedPo?.Vendor ?? "-",
            r.LinkedPo is null ? "No" : "Yes"
        }).ToList();

        var approvedCount = rowsData.Count(r => r.PrStatus is "Approved" or "ConvertedToPO");
        var convertedCount = rowsData.Count(r => r.LinkedPo != null);
        var conversionRate = approvedCount == 0 ? 0 : Math.Round((double)convertedCount * 100 / approvedCount, 2);

        return CreateProcurementReportVm(
            reportType,
            "PR to PO Conversion",
            "Trace approved purchase requests converted into purchase orders.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["PR Number", "PR Status", "PR Date", "Estimated Total", "PO Number", "PO Date", "Vendor", "Converted"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Total PRs", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Approved/Converted PRs", Value = approvedCount.ToString() },
                new ProcurementSummaryMetricVm { Label = "Converted PRs", Value = convertedCount.ToString() },
                new ProcurementSummaryMetricVm { Label = "Conversion Rate", Value = $"{conversionRate:N2}%" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildDepartmentPrSpendingReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseRequestLines
            .AsNoTracking()
            .Include(l => l.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.PurchaseRequest.RequestDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(l => l.PurchaseRequest.RequestDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(l => l.SuggestedVendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(l => l.PurchaseRequest.Department != null && l.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(l =>
                l.PurchaseRequest.PrNumber.ToLower().Contains(lowered) ||
                (l.Name != null && l.Name.ToLower().Contains(lowered)) ||
                (l.PurchaseRequest.Department != null && l.PurchaseRequest.Department.ToLower().Contains(lowered)));
        }

        var grouped = await query
            .GroupBy(l => l.PurchaseRequest.Department ?? "Unassigned")
            .Select(g => new
            {
                Department = g.Key,
                RequestCount = g.Select(x => x.PurchaseRequestId).Distinct().Count(),
                LineCount = g.Count(),
                EstimatedSpend = g.Sum(x => x.EstimatedTotal)
            })
            .OrderByDescending(x => x.EstimatedSpend)
            .ToListAsync();

        var rows = grouped.Select(g => (IReadOnlyList<string>)new[]
        {
            g.Department,
            g.RequestCount.ToString(),
            g.LineCount.ToString(),
            $"KES {g.EstimatedSpend:N2}"
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Department PR Spending",
            "Estimated procurement spend grouped by requesting department.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Department", "PR Count", "Line Items", "Estimated Spend"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Departments", Value = grouped.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Total PRs", Value = grouped.Sum(g => g.RequestCount).ToString() },
                new ProcurementSummaryMetricVm { Label = "Estimated Spend", Value = $"KES {grouped.Sum(g => g.EstimatedSpend):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildPoSummaryReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(po => po.OrderDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(po => po.OrderDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(po => po.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(po => po.PurchaseRequest != null &&
                                      po.PurchaseRequest.Department != null &&
                                      po.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(po =>
                po.PoNumber.ToLower().Contains(lowered) ||
                po.Vendor.CompanyName.ToLower().Contains(lowered) ||
                (po.PurchaseRequest != null && po.PurchaseRequest.PrNumber.ToLower().Contains(lowered)));
        }

        var rowsData = await query
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.Id)
            .Select(po => new
            {
                po.PoNumber,
                po.OrderDate,
                VendorName = po.Vendor.CompanyName,
                Department = po.PurchaseRequest != null ? po.PurchaseRequest.Department : null,
                Status = po.Status.ToString(),
                po.TotalAmount,
                po.Currency,
                PrNumber = po.PurchaseRequest != null ? po.PurchaseRequest.PrNumber : "-"
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.PoNumber,
            r.OrderDate.ToString("yyyy-MM-dd"),
            r.VendorName,
            string.IsNullOrWhiteSpace(r.Department) ? "-" : r.Department!,
            r.Status,
            $"{r.Currency} {r.TotalAmount:N2}",
            r.PrNumber ?? "-"
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "PO Summary",
            "Purchase order totals by status, vendor, and linked purchase request.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["PO Number", "Order Date", "Vendor", "Department", "Status", "Total Amount", "Linked PR"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Total POs", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Open POs", Value = rowsData.Count(r => r.Status is "Draft" or "Submitted" or "Invoiced" or "Approved").ToString() },
                new ProcurementSummaryMetricVm { Label = "Paid/Closed POs", Value = rowsData.Count(r => r.Status is "Paid" or "Closed" or "Received").ToString() },
                new ProcurementSummaryMetricVm { Label = "Total PO Spend", Value = $"KES {rowsData.Sum(r => r.TotalAmount):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildVendorPoSpendReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(po => po.OrderDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(po => po.OrderDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(po => po.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(po => po.PurchaseRequest != null &&
                                      po.PurchaseRequest.Department != null &&
                                      po.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(po =>
                po.Vendor.CompanyName.ToLower().Contains(lowered) ||
                po.Vendor.VendorCode.ToLower().Contains(lowered) ||
                po.PoNumber.ToLower().Contains(lowered));
        }

        var grouped = await query
            .GroupBy(po => new { po.VendorId, po.Vendor.CompanyName, po.Vendor.VendorCode })
            .Select(g => new
            {
                g.Key.VendorCode,
                g.Key.CompanyName,
                PoCount = g.Count(),
                TotalSpend = g.Sum(x => x.TotalAmount),
                ApprovedReceivedSpend = g.Where(x =>
                        x.Status == PurchaseOrderStatus.Paid ||
                        x.Status == PurchaseOrderStatus.Closed ||
                        x.Status == PurchaseOrderStatus.Received)
                    .Sum(x => x.TotalAmount),
                OpenCount = g.Count(x =>
                    x.Status == PurchaseOrderStatus.Draft ||
                    x.Status == PurchaseOrderStatus.Submitted ||
                    x.Status == PurchaseOrderStatus.Invoiced ||
                    x.Status == PurchaseOrderStatus.Approved)
            })
            .OrderByDescending(x => x.TotalSpend)
            .ToListAsync();

        var rows = grouped.Select(g => (IReadOnlyList<string>)new[]
        {
            g.VendorCode,
            g.CompanyName,
            g.PoCount.ToString(),
            $"KES {g.ApprovedReceivedSpend:N2}",
            $"KES {g.TotalSpend:N2}",
            g.OpenCount.ToString()
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Vendor PO Spend",
            "Total purchase order spend grouped by vendor.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Vendor Code", "Vendor", "PO Count", "Paid/Closed Spend", "Total PO Spend", "Open POs"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Vendors", Value = grouped.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Total PO Spend", Value = $"KES {grouped.Sum(g => g.TotalSpend):N2}" },
                new ProcurementSummaryMetricVm { Label = "Paid/Closed Spend", Value = $"KES {grouped.Sum(g => g.ApprovedReceivedSpend):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildOpenPurchaseOrdersReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.PurchaseRequest)
            .Where(po =>
                po.Status == PurchaseOrderStatus.Draft ||
                po.Status == PurchaseOrderStatus.Submitted ||
                po.Status == PurchaseOrderStatus.Invoiced ||
                po.Status == PurchaseOrderStatus.Approved)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(po => po.OrderDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(po => po.OrderDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(po => po.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(po => po.PurchaseRequest != null &&
                                      po.PurchaseRequest.Department != null &&
                                      po.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(po => po.PoNumber.ToLower().Contains(lowered) || po.Vendor.CompanyName.ToLower().Contains(lowered));
        }

        var today = DateTime.UtcNow.Date;
        var rowsData = await query
            .OrderBy(po => po.OrderDate)
            .Select(po => new
            {
                po.PoNumber,
                po.OrderDate,
                VendorName = po.Vendor.CompanyName,
                Department = po.PurchaseRequest != null ? po.PurchaseRequest.Department : null,
                Status = po.Status.ToString(),
                po.TotalAmount,
                AgeDays = (today - po.OrderDate.Date).Days
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.PoNumber,
            r.OrderDate.ToString("yyyy-MM-dd"),
            r.VendorName,
            string.IsNullOrWhiteSpace(r.Department) ? "-" : r.Department!,
            r.Status,
            $"KES {r.TotalAmount:N2}",
            r.AgeDays.ToString()
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Open Purchase Orders",
            "Purchase orders that are not yet received or cancelled.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["PO Number", "Order Date", "Vendor", "Department", "Status", "Total", "Age (Days)"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Open POs", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Open Value", Value = $"KES {rowsData.Sum(r => r.TotalAmount):N2}" },
                new ProcurementSummaryMetricVm { Label = "Avg Age (Days)", Value = rowsData.Count == 0 ? "0" : Math.Round(rowsData.Average(r => r.AgeDays), 1).ToString("N1") }
            ]);
    }

    private async Task<ProcurementReportVm> BuildVendorDeliveryPerformanceReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(po => po.OrderDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(po => po.OrderDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(po => po.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(po => po.PurchaseRequest != null &&
                                      po.PurchaseRequest.Department != null &&
                                      po.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(po => po.Vendor.CompanyName.ToLower().Contains(lowered) || po.PoNumber.ToLower().Contains(lowered));
        }

        var grouped = await query
            .GroupBy(po => new { po.VendorId, po.Vendor.CompanyName })
            .Select(g => new
            {
                Vendor = g.Key.CompanyName,
                TotalPos = g.Count(),
                Received = g.Count(x =>
                    x.Status == PurchaseOrderStatus.Paid ||
                    x.Status == PurchaseOrderStatus.Closed ||
                    x.Status == PurchaseOrderStatus.Received),
                Cancelled = g.Count(x => x.Status == PurchaseOrderStatus.Cancelled),
                Open = g.Count(x =>
                    x.Status == PurchaseOrderStatus.Draft ||
                    x.Status == PurchaseOrderStatus.Submitted ||
                    x.Status == PurchaseOrderStatus.Invoiced ||
                    x.Status == PurchaseOrderStatus.Approved)
            })
            .OrderByDescending(x => x.Received)
            .ToListAsync();

        var rows = grouped.Select(g =>
        {
            var rate = g.TotalPos == 0 ? 0 : Math.Round((double)g.Received * 100 / g.TotalPos, 2);
            return (IReadOnlyList<string>)new[]
            {
                g.Vendor,
                g.TotalPos.ToString(),
                g.Received.ToString(),
                g.Open.ToString(),
                g.Cancelled.ToString(),
                $"{rate:N2}%"
            };
        }).ToList();

        var avgRate = grouped.Count == 0 ? 0 : grouped.Average(g => g.TotalPos == 0 ? 0 : (double)g.Received / g.TotalPos) * 100;
        return CreateProcurementReportVm(
            reportType,
            "Vendor Delivery Performance",
            "Delivery performance based on PO completion (received vs open/cancelled).",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Vendor", "Total POs", "Completed", "Open", "Cancelled", "Delivery Rate"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Vendors", Value = grouped.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Total Completed", Value = grouped.Sum(g => g.Received).ToString() },
                new ProcurementSummaryMetricVm { Label = "Average Delivery Rate", Value = $"{avgRate:N2}%" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildInvoiceSummaryReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po!.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(i => i.InvoiceDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(i => i.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(i => i.PurchaseOrder != null &&
                                     i.PurchaseOrder.PurchaseRequest != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(lowered) ||
                i.Vendor.CompanyName.ToLower().Contains(lowered) ||
                (i.PurchaseOrder != null && i.PurchaseOrder.PoNumber.ToLower().Contains(lowered)));
        }

        var today = DateTime.UtcNow.Date;
        var rowsData = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new
            {
                i.InvoiceNumber,
                i.InvoiceDate,
                i.DueDate,
                Vendor = i.Vendor.CompanyName,
                Status = i.PaymentStatus == InvoicePaymentStatus.Pending && i.DueDate < today
                    ? InvoicePaymentStatus.Overdue.ToString()
                    : i.PaymentStatus.ToString(),
                i.Amount,
                i.Currency,
                PoNumber = i.PurchaseOrder != null ? i.PurchaseOrder.PoNumber : "-"
            })
            .ToListAsync();

        var rows = rowsData.Select(r => (IReadOnlyList<string>)new[]
        {
            r.InvoiceNumber,
            r.InvoiceDate.ToString("yyyy-MM-dd"),
            r.DueDate.ToString("yyyy-MM-dd"),
            r.Vendor,
            r.Status,
            $"{r.Currency} {r.Amount:N2}",
            r.PoNumber ?? "-"
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Invoice Summary",
            "Invoice ledger by vendor, due date, payment status, and linked PO.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Invoice", "Invoice Date", "Due Date", "Vendor", "Status", "Amount", "PO"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Total Invoices", Value = rowsData.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Pending/Overdue", Value = rowsData.Count(r => r.Status is "Pending" or "Overdue").ToString() },
                new ProcurementSummaryMetricVm { Label = "Paid", Value = rowsData.Count(r => r.Status == "Paid").ToString() },
                new ProcurementSummaryMetricVm { Label = "Total Invoice Value", Value = $"KES {rowsData.Sum(r => r.Amount):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildPendingOverdueInvoicesReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var baseReport = await BuildInvoiceSummaryReportAsync(reportType, fromDate, toExclusive, vendorId, department, search, vendorLabel);
        var rows = baseReport.Rows
            .Where(r => r.Count > 4 && (r[4] == InvoicePaymentStatus.Pending.ToString() || r[4] == InvoicePaymentStatus.Overdue.ToString()))
            .ToList();

        return CreateProcurementReportVm(
            reportType,
            "Pending/Overdue Invoices",
            "Outstanding invoices that need payment follow-up.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            baseReport.Headers,
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Outstanding Invoices", Value = rows.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Overdue Invoices", Value = rows.Count(r => r[4] == InvoicePaymentStatus.Overdue.ToString()).ToString() }
            ]);
    }

    private async Task<ProcurementReportVm> BuildVendorInvoiceSpendReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po!.PurchaseRequest)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(i => i.InvoiceDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(i => i.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(i => i.PurchaseOrder != null &&
                                     i.PurchaseOrder.PurchaseRequest != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(i => i.Vendor.CompanyName.ToLower().Contains(lowered) || i.InvoiceNumber.ToLower().Contains(lowered));
        }

        var today = DateTime.UtcNow.Date;
        var grouped = await query
            .GroupBy(i => new { i.VendorId, i.Vendor.CompanyName, i.Vendor.VendorCode })
            .Select(g => new
            {
                g.Key.VendorCode,
                Vendor = g.Key.CompanyName,
                InvoiceCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                PaidAmount = g.Where(x => x.PaymentStatus == InvoicePaymentStatus.Paid).Sum(x => x.Amount),
                OutstandingAmount = g.Where(x =>
                        x.PaymentStatus == InvoicePaymentStatus.Pending ||
                        x.PaymentStatus == InvoicePaymentStatus.Overdue ||
                        (x.PaymentStatus == InvoicePaymentStatus.Pending && x.DueDate < today))
                    .Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync();

        var rows = grouped.Select(g => (IReadOnlyList<string>)new[]
        {
            g.VendorCode,
            g.Vendor,
            g.InvoiceCount.ToString(),
            $"KES {g.TotalAmount:N2}",
            $"KES {g.PaidAmount:N2}",
            $"KES {g.OutstandingAmount:N2}"
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Vendor Invoice Spend",
            "Invoice value and payment balance grouped by vendor.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Vendor Code", "Vendor", "Invoices", "Total Billed", "Paid Amount", "Outstanding Amount"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Vendors", Value = grouped.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Total Billed", Value = $"KES {grouped.Sum(g => g.TotalAmount):N2}" },
                new ProcurementSummaryMetricVm { Label = "Outstanding", Value = $"KES {grouped.Sum(g => g.OutstandingAmount):N2}" }
            ]);
    }

    private async Task<ProcurementReportVm> BuildInvoiceVsPoMatchingReportAsync(
        ProcurementReportType reportType,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel)
    {
        var query = _context.VendorInvoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.PurchaseOrder)
                .ThenInclude(po => po!.PurchaseRequest)
            .Where(i => i.PurchaseOrderId != null)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(i => i.InvoiceDate < toExclusive.Value);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(i => i.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var loweredDepartment = department.ToLower();
            query = query.Where(i => i.PurchaseOrder != null &&
                                     i.PurchaseOrder.PurchaseRequest != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department != null &&
                                     i.PurchaseOrder.PurchaseRequest.Department.ToLower().Contains(loweredDepartment));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(lowered) ||
                i.PurchaseOrder!.PoNumber.ToLower().Contains(lowered) ||
                i.Vendor.CompanyName.ToLower().Contains(lowered));
        }

        var rowsData = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new
            {
                i.InvoiceNumber,
                PoNumber = i.PurchaseOrder!.PoNumber,
                Vendor = i.Vendor.CompanyName,
                InvoiceAmount = i.Amount,
                PoAmount = i.PurchaseOrder!.TotalAmount
            })
            .ToListAsync();

        var rows = rowsData.Select(r =>
        {
            var diff = r.InvoiceAmount - r.PoAmount;
            var matchStatus = Math.Abs(diff) < 0.01m ? "Matched" : (diff > 0 ? "Over Billed" : "Under Billed");
            return (IReadOnlyList<string>)new[]
            {
                r.InvoiceNumber,
                r.PoNumber,
                r.Vendor,
                $"KES {r.InvoiceAmount:N2}",
                $"KES {r.PoAmount:N2}",
                $"KES {diff:N2}",
                matchStatus
            };
        }).ToList();

        return CreateProcurementReportVm(
            reportType,
            "Invoice vs PO Matching",
            "Compare invoice amounts against linked purchase order totals.",
            fromDate,
            toExclusive,
            vendorId,
            department,
            search,
            vendorLabel,
            ["Invoice", "PO", "Vendor", "Invoice Amount", "PO Amount", "Variance", "Match Status"],
            rows,
            [
                new ProcurementSummaryMetricVm { Label = "Compared Invoices", Value = rows.Count.ToString() },
                new ProcurementSummaryMetricVm { Label = "Matched", Value = rows.Count(r => r[6] == "Matched").ToString() },
                new ProcurementSummaryMetricVm { Label = "Mismatched", Value = rows.Count(r => r[6] != "Matched").ToString() },
                new ProcurementSummaryMetricVm { Label = "Net Variance", Value = $"KES {rowsData.Sum(r => r.InvoiceAmount - r.PoAmount):N2}" }
            ]);
    }

    private static ProcurementReportVm CreateProcurementReportVm(
        ProcurementReportType reportType,
        string title,
        string description,
        DateTime? fromDate,
        DateTime? toExclusive,
        int? vendorId,
        string? department,
        string? search,
        string vendorLabel,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<ProcurementSummaryMetricVm> summaryCards)
    {
        return new ProcurementReportVm
        {
            ReportType = reportType,
            Title = title,
            Description = description,
            From = fromDate,
            To = toExclusive?.AddDays(-1),
            VendorId = vendorId,
            Department = department,
            Search = search,
            VendorLabel = vendorLabel,
            Headers = headers,
            Rows = rows,
            SummaryCards = summaryCards
        };
    }

    private static byte[] BuildExcelWorkbook(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, Math.Max(headers.Count, 1)).Merge().Style.Font.SetBold().Font.SetFontSize(14);

        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(3, i + 1).Value = headers[i];
            sheet.Cell(3, i + 1).Style.Font.SetBold();
            sheet.Cell(3, i + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        var rowIndex = 4;
        foreach (var row in rows)
        {
            for (var col = 0; col < row.Count; col++)
            {
                sheet.Cell(rowIndex, col + 1).Value = row[col];
            }

            rowIndex++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetReportFileNamePrefix(ProcurementReportType reportType)
    {
        return reportType switch
        {
            ProcurementReportType.PrSummary => "pr-summary",
            ProcurementReportType.PendingApprovalPrs => "pending-approval-prs",
            ProcurementReportType.PrToPoConversion => "pr-po-conversion",
            ProcurementReportType.DepartmentPrSpending => "department-pr-spending",
            ProcurementReportType.PoSummary => "po-summary",
            ProcurementReportType.VendorPoSpend => "vendor-po-spend",
            ProcurementReportType.OpenPurchaseOrders => "open-purchase-orders",
            ProcurementReportType.VendorDeliveryPerformance => "vendor-delivery-performance",
            ProcurementReportType.InvoiceSummary => "invoice-summary",
            ProcurementReportType.PendingOverdueInvoices => "pending-overdue-invoices",
            ProcurementReportType.VendorInvoiceSpend => "vendor-invoice-spend",
            ProcurementReportType.InvoiceVsPoMatching => "invoice-po-matching",
            _ => "procurement-report"
        };
    }

    private static string GetEnumDisplayName(Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault();
        return display?.Name ?? value.ToString();
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
