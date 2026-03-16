using AssetTracker.Data;
using AssetTracker.Models;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class PurchaseRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IVendorCodeGenerator _codeGenerator;
    private readonly IPurchaseOrderStatusSyncService _purchaseOrderStatusSyncService;

    public PurchaseRequestsController(
        ApplicationDbContext context,
        IVendorCodeGenerator codeGenerator,
        IPurchaseOrderStatusSyncService purchaseOrderStatusSyncService)
    {
        _context = context;
        _codeGenerator = codeGenerator;
        _purchaseOrderStatusSyncService = purchaseOrderStatusSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, PurchaseRequestStatus? status = null)
    {
        var normalizedQ = q?.Trim();
        var query = _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.RequestedByStaff)
            .Include(pr => pr.Lines)
            .Include(pr => pr.PurchaseOrders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            query = query.Where(pr =>
                pr.PrNumber.ToLower().Contains(lowered) ||
                (pr.Department != null && pr.Department.ToLower().Contains(lowered)) ||
                (pr.RequestedByStaff != null && pr.RequestedByStaff.FullName.ToLower().Contains(lowered)));
        }

        if (status.HasValue)
        {
            query = query.Where(pr => pr.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(pr => pr.CreatedAt)
            .ThenByDescending(pr => pr.Id)
            .Select(pr => new PurchaseRequestListItemVm
            {
                Id = pr.Id,
                PrNumber = pr.PrNumber,
                RequestDate = pr.RequestDate,
                RequestedBy = pr.RequestedByStaff != null ? pr.RequestedByStaff.FullName : "-",
                Department = string.IsNullOrWhiteSpace(pr.Department) ? "-" : pr.Department,
                Priority = pr.Priority,
                Status = pr.Status,
                NeededByDate = pr.NeededByDate,
                EstimatedTotal = pr.Lines.Sum(l => (decimal?)l.EstimatedTotal) ?? 0m,
                LinkedPurchaseOrderId = pr.PurchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Select(po => (int?)po.Id)
                    .FirstOrDefault(),
                LinkedPoNumber = pr.PurchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Select(po => po.PoNumber)
                    .FirstOrDefault(),
                CanEdit = !pr.PurchaseOrders.Any() &&
                    (pr.Status == PurchaseRequestStatus.Draft || pr.Status == PurchaseRequestStatus.Approved)
            })
            .ToListAsync();

        var vm = new PurchaseRequestIndexVm
        {
            Query = normalizedQ,
            StatusFilter = status,
            TotalRequests = await _context.PurchaseRequests.AsNoTracking().CountAsync(),
            PendingApproval = await _context.PurchaseRequests.AsNoTracking()
                .CountAsync(pr => pr.Status == PurchaseRequestStatus.PendingApproval || pr.Status == PurchaseRequestStatus.Submitted),
            Approved = await _context.PurchaseRequests.AsNoTracking().CountAsync(pr => pr.Status == PurchaseRequestStatus.Approved),
            ConvertedToPo = await _context.PurchaseRequests.AsNoTracking().CountAsync(pr => pr.Status == PurchaseRequestStatus.ConvertedToPO),
            Items = items
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? suggestedVendorId = null)
    {
        var vm = new PurchaseRequestUpsertVm
        {
            RequestDate = DateTime.UtcNow.Date
        };
        EnsureMinimumLineRows(vm);

        if (suggestedVendorId.HasValue)
        {
            vm.Lines[0].SuggestedVendorId = suggestedVendorId;
        }

        await PopulateLookupsAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PurchaseRequestUpsertVm vm,
        [FromForm(Name = "action")] string? formAction = null,
        string? submitAction = null)
    {
        var selectedAction = ResolveSubmitAction(formAction, submitAction);
        var lines = await ValidateAndNormalizeLinesAsync(vm);
        ValidateRequestDates(vm);

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(vm);
            await PopulateLookupsAsync();
            return View(vm);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var status = string.Equals(selectedAction, "submit", StringComparison.OrdinalIgnoreCase)
            ? PurchaseRequestStatus.Approved
            : PurchaseRequestStatus.Draft;
        var (requestedByStaffId, requestDepartment) = await ResolveRequesterContextAsync();

        var request = new PurchaseRequest
        {
            PrNumber = await _codeGenerator.NextPurchaseRequestNumberAsync(),
            RequestDate = vm.RequestDate,
            RequestedByStaffId = requestedByStaffId,
            Department = requestDepartment,
            NeededByDate = vm.NeededByDate,
            Justification = null,
            Priority = PurchaseRequestPriority.Normal,
            Status = status,
            ApprovedByUserId = status == PurchaseRequestStatus.Approved ? currentUserId : null,
            ApprovedAt = status == PurchaseRequestStatus.Approved ? DateTime.UtcNow : null,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            Lines = lines
        };

        _context.PurchaseRequests.Add(request);
        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = status == PurchaseRequestStatus.Approved
            ? "Purchase request submitted and auto-approved."
            : "Purchase request saved as draft.";

        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var request = await _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.Lines)
            .Include(pr => pr.PurchaseOrders)
            .FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        var hasLinkedPo = request.PurchaseOrders.Any();
        if (!CanEdit(request.Status, hasLinkedPo))
        {
            TempData["PrError"] = "This purchase request can no longer be edited because it has already been converted to a purchase order.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new PurchaseRequestUpsertVm
        {
            Id = request.Id,
            PrNumber = request.PrNumber,
            RequestDate = request.RequestDate,
            NeededByDate = request.NeededByDate,
            Status = request.Status,
            Notes = request.Notes,
            Lines = request.Lines
                .OrderBy(l => l.Id)
                .Select(l => new PurchaseRequestLineInputVm
                {
                    Id = l.Id,
                    LineType = l.LineType,
                    Name = l.Name,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    EstimatedUnitCost = l.EstimatedUnitCost,
                    EstimatedTotal = l.EstimatedTotal,
                    SuggestedVendorId = l.SuggestedVendorId,
                    Notes = l.Notes
                })
                .ToList()
        };

        if (vm.Lines.Count == 0)
        {
            vm.Lines.Add(new PurchaseRequestLineInputVm());
        }
        EnsureMinimumLineRows(vm);

        await PopulateLookupsAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseRequestUpsertVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        var request = await _context.PurchaseRequests
            .Include(pr => pr.Lines)
            .Include(pr => pr.PurchaseOrders)
            .FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        var hasLinkedPo = request.PurchaseOrders.Any();
        if (!CanEdit(request.Status, hasLinkedPo))
        {
            TempData["PrError"] = "This purchase request can no longer be edited because it has already been converted to a purchase order.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var lines = await ValidateAndNormalizeLinesAsync(vm);
        ValidateRequestDates(vm);

        if (!ModelState.IsValid)
        {
            EnsureMinimumLineRows(vm);
            await PopulateLookupsAsync();
            return View(vm);
        }

        request.RequestDate = vm.RequestDate;
        request.NeededByDate = vm.NeededByDate;
        request.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
        request.RejectedReason = null;

        _context.PurchaseRequestLines.RemoveRange(request.Lines);
        request.Lines.Clear();
        foreach (var line in lines)
        {
            request.Lines.Add(line);
        }

        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = "Purchase request updated successfully.";

        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _context.PurchaseRequests
            .AsNoTracking()
            .Include(pr => pr.RequestedByStaff)
            .Include(pr => pr.Lines)
                .ThenInclude(l => l.SuggestedVendor)
            .FirstOrDefaultAsync(pr => pr.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        var linkedPo = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Vendor)
            .Include(po => po.Lines)
            .OrderByDescending(po => po.CreatedAt)
            .FirstOrDefaultAsync(po => po.PurchaseRequestId == request.Id);

        var approvedByLabel = "-";
        if (!string.IsNullOrWhiteSpace(request.ApprovedByUserId))
        {
            approvedByLabel = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == request.ApprovedByUserId)
                .Select(u => u.Email ?? u.UserName ?? "-")
                .FirstOrDefaultAsync() ?? "-";
        }

        var vm = new PurchaseRequestDetailsVm
        {
            Request = request,
            Lines = request.Lines.OrderBy(l => l.Id).ToList(),
            LinkedPurchaseOrder = linkedPo,
            RequestedByLabel = request.RequestedByStaff?.FullName ?? "-",
            ApprovedByLabel = approvedByLabel,
            EstimatedTotal = request.Lines.Sum(l => (decimal?)l.EstimatedTotal) ?? 0m
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var request = await _context.PurchaseRequests.FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status is not PurchaseRequestStatus.Draft)
        {
            TempData["PrError"] = "Only draft requests can be submitted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        request.Status = PurchaseRequestStatus.Approved;
        request.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        request.ApprovedAt = DateTime.UtcNow;
        request.RejectedReason = null;
        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = "Purchase request submitted and auto-approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int id)
    {
        var request = await _context.PurchaseRequests.FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status is not (PurchaseRequestStatus.PendingApproval or PurchaseRequestStatus.Submitted))
        {
            TempData["PrError"] = "Only pending requests can be approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        request.Status = PurchaseRequestStatus.Approved;
        request.ApprovedAt = DateTime.UtcNow;
        request.ApprovedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        request.RejectedReason = null;
        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = "Purchase request approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var request = await _context.PurchaseRequests.FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status is not (PurchaseRequestStatus.PendingApproval or PurchaseRequestStatus.Submitted))
        {
            TempData["PrError"] = "Only pending requests can be rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["PrError"] = "Provide a rejection reason.";
            return RedirectToAction(nameof(Details), new { id });
        }

        request.Status = PurchaseRequestStatus.Rejected;
        request.RejectedReason = reason.Trim();
        request.ApprovedAt = null;
        request.ApprovedByUserId = null;
        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = "Purchase request rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var request = await _context.PurchaseRequests.FirstOrDefaultAsync(pr => pr.Id == id);
        if (request is null)
        {
            return NotFound();
        }

        if (request.Status == PurchaseRequestStatus.ConvertedToPO)
        {
            TempData["PrError"] = "Converted requests cannot be closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (request.Status == PurchaseRequestStatus.Closed)
        {
            TempData["PrError"] = "Purchase request is already closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        request.Status = PurchaseRequestStatus.Closed;
        await _context.SaveChangesAsync();

        TempData["PrSuccess"] = "Purchase request closed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePo(int id)
    {
        var request = await _context.PurchaseRequests
            .Include(pr => pr.Lines)
            .FirstOrDefaultAsync(pr => pr.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        if (request.Status != PurchaseRequestStatus.Approved)
        {
            TempData["PrError"] = "Only approved purchase requests can be converted to purchase orders.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var existingPo = await _context.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(po => po.PurchaseRequestId == request.Id);
        if (existingPo is not null)
        {
            request.Status = PurchaseRequestStatus.ConvertedToPO;
            await _context.SaveChangesAsync();
            TempData["PrError"] = $"This request is already linked to PO {existingPo.PoNumber}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (request.Lines.Count == 0)
        {
            TempData["PrError"] = "Cannot create a PO from a request with no line items.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var distinctVendorIds = request.Lines
            .Where(l => l.SuggestedVendorId.HasValue)
            .Select(l => l.SuggestedVendorId!.Value)
            .Distinct()
            .ToList();

        if (distinctVendorIds.Count != 1)
        {
            TempData["PrError"] = "To convert a PR to PO, all lines must have the same suggested vendor.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vendorId = distinctVendorIds[0];
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == vendorId && !v.IsArchived);
        if (vendor is null)
        {
            TempData["PrError"] = "The suggested vendor no longer exists.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (vendor.Status == VendorStatus.Blacklisted)
        {
            TempData["PrError"] = "Cannot create a PO for a blacklisted vendor.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var poNumber = await _codeGenerator.NextPurchaseOrderNumberAsync();
        var poLines = request.Lines.Select(line =>
        {
            var quantity = line.Quantity.GetValueOrDefault() > 0
                ? line.Quantity!.Value
                : 1;
            var lineTotal = line.EstimatedTotal.GetValueOrDefault() > 0
                ? line.EstimatedTotal!.Value
                : 0m;
            var unitCost = line.EstimatedUnitCost.GetValueOrDefault() > 0
                ? line.EstimatedUnitCost!.Value
                : (lineTotal > 0 && quantity > 0 ? Math.Round(lineTotal / quantity, 2) : 0m);

            if (lineTotal <= 0)
            {
                lineTotal = unitCost * quantity;
            }

            return new PurchaseOrderLine
            {
                ItemName = line.Name,
                Quantity = quantity,
                UnitPrice = unitCost,
                LineTotal = lineTotal
            };
        }).ToList();

        var totalAmount = poLines.Sum(l => l.LineTotal);
        var po = new PurchaseOrder
        {
            PoNumber = poNumber,
            VendorId = vendorId,
            PurchaseRequestId = request.Id,
            OrderDate = DateTime.UtcNow.Date,
            TotalAmount = totalAmount,
            Currency = "KES",
            Status = PurchaseOrderStatus.Submitted,
            Notes = $"Auto-created from {request.PrNumber}",
            CreatedAt = DateTime.UtcNow,
            Lines = poLines
        };

        await using var tx = await _context.Database.BeginTransactionAsync();
        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync();
        await _purchaseOrderStatusSyncService.SyncPurchaseOrderStatusAsync(po.Id);

        request.Status = PurchaseRequestStatus.ConvertedToPO;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["PrSuccess"] = $"Purchase order {po.PoNumber} created successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static bool CanEdit(PurchaseRequestStatus status, bool hasLinkedPo)
    {
        return !hasLinkedPo
            && (status == PurchaseRequestStatus.Draft || status == PurchaseRequestStatus.Approved);
    }

    private void ValidateRequestDates(PurchaseRequestUpsertVm vm)
    {
        if (vm.NeededByDate.HasValue && vm.NeededByDate.Value.Date < vm.RequestDate.Date)
        {
            ModelState.AddModelError(nameof(PurchaseRequestUpsertVm.NeededByDate), "Needed by date cannot be before request date.");
        }
    }

    private async Task<List<PurchaseRequestLine>> ValidateAndNormalizeLinesAsync(PurchaseRequestUpsertVm vm)
    {
        vm.Lines ??= new List<PurchaseRequestLineInputVm>();
        var sourceLines = vm.Lines
            .Where(l =>
                !string.IsNullOrWhiteSpace(l.Name) ||
                !string.IsNullOrWhiteSpace(l.Description))
            .ToList();

        if (sourceLines.Count == 0)
        {
            ModelState.AddModelError(nameof(PurchaseRequestUpsertVm.Lines), "At least one item line is required.");
            return [];
        }

        var suggestedVendorIds = sourceLines
            .Where(l => l.SuggestedVendorId.HasValue)
            .Select(l => l.SuggestedVendorId!.Value)
            .Distinct()
            .ToList();

        if (suggestedVendorIds.Count > 0)
        {
            var validVendorIds = await _context.Vendors
                .AsNoTracking()
                .Where(v => !v.IsArchived && suggestedVendorIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync();

            var invalidVendorIds = suggestedVendorIds.Except(validVendorIds).ToList();
            if (invalidVendorIds.Count > 0)
            {
                ModelState.AddModelError(nameof(PurchaseRequestUpsertVm.Lines), "One or more suggested vendors are invalid.");
            }
        }

        var lines = new List<PurchaseRequestLine>();
        for (var i = 0; i < sourceLines.Count; i++)
        {
            var line = sourceLines[i];

            if (!line.LineType.HasValue)
            {
                ModelState.AddModelError(nameof(PurchaseRequestUpsertVm.Lines), $"Line type is required for line {i + 1}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line.Name) && string.IsNullOrWhiteSpace(line.Description))
            {
                ModelState.AddModelError(nameof(PurchaseRequestUpsertVm.Lines), $"Name or description is required for line {i + 1}.");
                continue;
            }

            var isService = line.LineType == PurchaseRequestLineType.Service;
            int? quantity;
            decimal? estimatedUnitCost;
            decimal? estimatedTotal;

            if (isService)
            {
                // Services should not carry forced numeric defaults.
                quantity = null;
                estimatedUnitCost = null;
                estimatedTotal = line.EstimatedTotal.HasValue
                    ? decimal.Round(line.EstimatedTotal.Value, 2)
                    : null;

                if (!estimatedTotal.HasValue || estimatedTotal.Value <= 0)
                {
                    ModelState.AddModelError($"Items[{i}].EstimatedTotal", "Service total is required.");
                    continue;
                }
            }
            else
            {
                var normalizedQuantity = line.Quantity.GetValueOrDefault();
                if (normalizedQuantity <= 0)
                {
                    normalizedQuantity = 1;
                }

                var normalizedUnitCost = line.EstimatedUnitCost.GetValueOrDefault();
                if (normalizedUnitCost < 0)
                {
                    normalizedUnitCost = 0m;
                }

                quantity = normalizedQuantity;
                estimatedUnitCost = decimal.Round(normalizedUnitCost, 2);
                estimatedTotal = decimal.Round(quantity.Value * estimatedUnitCost.Value, 2);
            }

            var normalizedName = !string.IsNullOrWhiteSpace(line.Name)
                ? line.Name.Trim()
                : line.Description!.Trim();

            lines.Add(new PurchaseRequestLine
            {
                LineType = line.LineType.Value,
                Name = normalizedName.Length > 200 ? normalizedName[..200] : normalizedName,
                Description = string.IsNullOrWhiteSpace(line.Description) ? null : line.Description.Trim(),
                Quantity = quantity,
                EstimatedUnitCost = estimatedUnitCost,
                EstimatedTotal = estimatedTotal,
                SuggestedVendorId = line.SuggestedVendorId,
                Notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim()
            });
        }

        return lines;
    }

    private async Task PopulateLookupsAsync()
    {
        var vendorOptions = await _context.Vendors
            .AsNoTracking()
            .Where(v => !v.IsArchived)
            .OrderBy(v => v.CompanyName)
            .Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = $"{v.CompanyName} ({v.VendorCode})"
            })
            .ToListAsync();
        vendorOptions.Insert(0, new SelectListItem { Value = string.Empty, Text = "No suggestion" });

        ViewData["SuggestedVendorOptions"] = vendorOptions;
    }

    private static string ResolveSubmitAction(string? action, string? submitAction)
    {
        if (!string.IsNullOrWhiteSpace(action))
        {
            return action.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(submitAction))
        {
            return submitAction.Trim().ToLowerInvariant();
        }

        return "draft";
    }

    private async Task<(int? StaffId, string? Department)> ResolveRequesterContextAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return (null, null);
        }

        var staff = await _context.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == currentUserId);

        if (staff is null)
        {
            return (null, null);
        }

        return (staff.Id, string.IsNullOrWhiteSpace(staff.Department) ? null : staff.Department.Trim());
    }

    private static void EnsureMinimumLineRows(PurchaseRequestUpsertVm vm, int minimumRows = 1)
    {
        vm.Lines ??= new List<PurchaseRequestLineInputVm>();
        if (vm.Lines.Count == 0)
        {
            vm.Lines.Add(new PurchaseRequestLineInputVm());
        }

        while (vm.Lines.Count < minimumRows)
        {
            vm.Lines.Add(new PurchaseRequestLineInputVm());
        }
    }
}
