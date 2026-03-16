using AssetTracker.Data;
using AssetTracker.Models;
using AssetTracker.Models.Assets;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class AssetsController : Controller
{
    private const string SoftwareTopLevelKey = "software";
    private const string PhysicalTopLevelKey = "physical";
    private const string CannotRetireAssignedAssetMessage = "Assigned assets cannot be retired. Please return or unassign the asset first.";
    private readonly ApplicationDbContext _context;
    private readonly IAssetTaxonomyService _assetTaxonomyService;

    public AssetsController(ApplicationDbContext context, IAssetTaxonomyService assetTaxonomyService)
    {
        _context = context;
        _assetTaxonomyService = assetTaxonomyService;
    }

    public async Task<IActionResult> Index(
        string? q,
        string? group,
        string? category,
        string? subCategory,
        AssetStatus? status = null,
        int? vendorId = null,
        SoftwareStatus? softwareStatus = null,
        bool expiringSoon = false,
        SoftwareBillingCycle? billingCycle = null,
        bool? autoRenew = null,
        SoftwareDeploymentEnvironment? deploymentEnvironment = null,
        string? assignedTo = null,
        string? installedOn = null,
        string? type = null)
    {
        var normalizedQ = q?.Trim();
        var normalizedAssignedTo = assignedTo?.Trim();
        var normalizedInstalledOn = installedOn?.Trim();

        if (!_assetTaxonomyService.TryNormalizeSelection(group, category, subCategory, out var selection, out _))
        {
            selection = new AssetTaxonomySelection();
        }

        var query = _context.Assets
            .AsNoTracking()
            .Include(a => a.Vendor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(selection.TopLevelCategory))
        {
            query = query.Where(a => a.TopLevelCategory == selection.TopLevelCategory);
        }

        if (!string.IsNullOrWhiteSpace(selection.Category))
        {
            if (string.Equals(selection.TopLevelCategory, "hardware", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(selection.SubCategory))
            {
                var normalizedHardwareCategory = selection.Category.Trim().ToLowerInvariant();
                query = normalizedHardwareCategory switch
                {
                    "laptops" => query.Where(a => a.Category == selection.Category
                                                  || ((a.Category == null || a.Category == string.Empty)
                                                      && (a.TopLevelCategory == null
                                                          || a.TopLevelCategory == string.Empty
                                                          || a.TopLevelCategory == "hardware")
                                                      && a.AssetType == AssetType.Laptop)),
                    "desktops" => query.Where(a => a.Category == selection.Category
                                                   || ((a.Category == null || a.Category == string.Empty)
                                                       && (a.TopLevelCategory == null
                                                           || a.TopLevelCategory == string.Empty
                                                           || a.TopLevelCategory == "hardware")
                                                       && (a.AssetType == AssetType.Desktop
                                                           || a.AssetType == AssetType.SystemUnit))),
                    "monitors" => query.Where(a => a.Category == selection.Category
                                                   || ((a.Category == null || a.Category == string.Empty)
                                                       && (a.TopLevelCategory == null
                                                           || a.TopLevelCategory == string.Empty
                                                           || a.TopLevelCategory == "hardware")
                                                       && a.AssetType == AssetType.Monitor)),
                    "printers" => query.Where(a => a.Category == selection.Category
                                                   || ((a.Category == null || a.Category == string.Empty)
                                                       && (a.TopLevelCategory == null
                                                           || a.TopLevelCategory == string.Empty
                                                           || a.TopLevelCategory == "hardware")
                                                       && a.AssetType == AssetType.Printer)),
                    "servers" => query.Where(a => a.Category == selection.Category
                                                  || ((a.Category == null || a.Category == string.Empty)
                                                      && (a.TopLevelCategory == null
                                                          || a.TopLevelCategory == string.Empty
                                                          || a.TopLevelCategory == "hardware")
                                                      && (a.AssetType == AssetType.Router
                                                          || a.AssetType == AssetType.Switch))),
                    "mobile-phones" => query.Where(a => (a.AssetType == AssetType.MobilePhone
                                                          && (a.TopLevelCategory == null
                                                              || a.TopLevelCategory == string.Empty
                                                              || a.TopLevelCategory == "hardware"))
                                                         || a.Category == selection.Category),
                    "peripherals" => query.Where(a => (a.Category == selection.Category
                                                       && a.AssetType != AssetType.MobilePhone)
                                                      || ((a.Category == null || a.Category == string.Empty)
                                                          && (a.TopLevelCategory == null
                                                              || a.TopLevelCategory == string.Empty
                                                              || a.TopLevelCategory == "hardware")
                                                          && (a.AssetType == AssetType.Keyboard
                                                              || a.AssetType == AssetType.Pda
                                                              || a.AssetType == AssetType.HcsCraneScale))),
                    _ => query.Where(a => a.Category == selection.Category)
                };
            }
            else
            {
                query = query.Where(a => a.Category == selection.Category);
            }
        }

        if (!string.IsNullOrWhiteSpace(selection.SubCategory))
        {
            query = query.Where(a => a.SubCategory == selection.SubCategory);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (TryParseAssetTypeFilter(type, out var assetTypeFilter))
        {
            query = query.Where(a => a.AssetType == assetTypeFilter);
        }

        var isSoftwareScope = string.Equals(selection.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase);
        var isPhysicalScope = string.Equals(selection.TopLevelCategory, PhysicalTopLevelKey, StringComparison.OrdinalIgnoreCase);
        if (isSoftwareScope)
        {
            if (vendorId.HasValue)
            {
                query = query.Where(a => a.VendorId == vendorId.Value);
            }

            if (softwareStatus.HasValue)
            {
                query = query.Where(a => a.SoftwareStatus == softwareStatus.Value);
            }

            if (billingCycle.HasValue)
            {
                query = query.Where(a => a.BillingCycle == billingCycle.Value);
            }

            if (autoRenew.HasValue)
            {
                query = query.Where(a => a.AutoRenew == autoRenew.Value);
            }

            if (deploymentEnvironment.HasValue)
            {
                query = query.Where(a => a.DeploymentEnvironment == deploymentEnvironment.Value);
            }

            if (expiringSoon)
            {
                var soonDate = DateTime.UtcNow.Date.AddDays(30);
                query = query.Where(a => a.ExpiryDate.HasValue && a.ExpiryDate.Value.Date <= soonDate);
            }

            if (!string.IsNullOrWhiteSpace(normalizedAssignedTo))
            {
                var loweredAssigned = normalizedAssignedTo.ToLower();
                query = query.Where(a => a.AssignedToUserOrDepartment != null && a.AssignedToUserOrDepartment.ToLower().Contains(loweredAssigned));
            }

            if (!string.IsNullOrWhiteSpace(normalizedInstalledOn))
            {
                var loweredInstalled = normalizedInstalledOn.ToLower();
                query = query.Where(a => a.InstalledOn != null && a.InstalledOn.ToLower().Contains(loweredInstalled));
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLower();
            if (isPhysicalScope)
            {
                query = query.Where(a =>
                    a.AssetTag.ToLower().Contains(lowered) ||
                    a.AssetType.ToString().ToLower().Contains(lowered) ||
                    a.Status.ToString().ToLower().Contains(lowered) ||
                    a.Location.ToLower().Contains(lowered));
            }
            else
            {
                query = query.Where(a =>
                    a.AssetTag.ToLower().Contains(lowered) ||
                    a.Brand.ToLower().Contains(lowered) ||
                    a.Model.ToLower().Contains(lowered) ||
                    a.SerialNumber.ToLower().Contains(lowered) ||
                    a.Location.ToLower().Contains(lowered) ||
                    a.Condition.ToLower().Contains(lowered) ||
                    (a.TopLevelCategory != null && a.TopLevelCategory.ToLower().Contains(lowered)) ||
                    (a.Category != null && a.Category.ToLower().Contains(lowered)) ||
                    (a.SubCategory != null && a.SubCategory.ToLower().Contains(lowered)) ||
                    (a.SoftwareName != null && a.SoftwareName.ToLower().Contains(lowered)) ||
                    (a.SoftwareVersion != null && a.SoftwareVersion.ToLower().Contains(lowered)) ||
                    (a.AssignedToUserOrDepartment != null && a.AssignedToUserOrDepartment.ToLower().Contains(lowered)) ||
                    (a.InstalledOn != null && a.InstalledOn.ToLower().Contains(lowered)) ||
                    (a.PlanOrTier != null && a.PlanOrTier.ToLower().Contains(lowered)) ||
                    (a.LicenseType != null && a.LicenseType.ToLower().Contains(lowered)));
            }
        }

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        var vm = new AssetIndexVm
        {
            Query = normalizedQ,
            Heading = _assetTaxonomyService.BuildHeading(selection),
            Selection = selection,
            Breadcrumbs = _assetTaxonomyService.BuildBreadcrumbs(selection, Url),
            TotalCount = items.Count,
            IsSoftwareScope = isSoftwareScope,
            IsPhysicalScope = isPhysicalScope,
            VendorId = vendorId,
            SoftwareStatus = softwareStatus,
            ExpiringSoon = expiringSoon,
            BillingCycle = billingCycle,
            AutoRenew = autoRenew,
            DeploymentEnvironment = deploymentEnvironment,
            AssignedTo = normalizedAssignedTo,
            InstalledOn = normalizedInstalledOn,
            Items = items
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var asset = await _context.Assets
            .AsNoTracking()
            .Include(a => a.Vendor)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        var activeAssignment = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssetId == asset.Id &&
                        (a.Status == AssignmentStatus.Active
                         || a.Status == AssignmentStatus.PendingAcceptance
                         || a.Status == AssignmentStatus.Accepted
                         || a.Status == AssignmentStatus.ReturnRequested))
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                IssueDate = (DateTime?)a.AssignedAt,
                CurrentUser = a.StaffProfile.FullName
            })
            .FirstOrDefaultAsync();

        var latestIssueDate = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssetId == asset.Id)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => (DateTime?)a.AssignedAt)
            .FirstOrDefaultAsync();

        var latestReturnedDate = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssetId == asset.Id &&
                        (a.ReturnedApprovedAt.HasValue
                         || a.Status == AssignmentStatus.Returned
                         || a.Status == AssignmentStatus.ReturnedApproved))
            .OrderByDescending(a => a.ReturnedApprovedAt ?? a.AssignedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.ReturnedApprovedAt ?? (DateTime?)a.AssignedAt)
            .FirstOrDefaultAsync();

        var vm = new AssetDetailsVm
        {
            Asset = asset,
            IssueDate = activeAssignment?.IssueDate ?? latestIssueDate,
            ReturnedDate = latestReturnedDate,
            CurrentUser = activeAssignment?.CurrentUser,
            HasActiveAssignment = activeAssignment is not null
        };

        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(string? group, string? category, string? subCategory)
    {
        var normalizedCreateSelection = NormalizeCreateSelection(group, category, subCategory);
        AssetTaxonomySelection selection = new();
        if (_assetTaxonomyService.TryNormalizeSelection(
            normalizedCreateSelection.Group,
            normalizedCreateSelection.Category,
            normalizedCreateSelection.SubCategory,
            out var parsedSelection,
            out _))
        {
            selection = parsedSelection;
        }

        if (IsSoftwareSelection(selection))
        {
            return RedirectToSoftwareCreate(selection.Category);
        }

        var asset = new Asset();
        if (!selection.IsEmpty)
        {
            asset.TopLevelCategory = selection.TopLevelCategory;
            asset.Category = selection.Category;
            asset.SubCategory = selection.SubCategory;

            if (IsHardwareSelection(selection))
            {
                asset.AssetType = selection.Category?.ToLowerInvariant() switch
                {
                    "laptops" => AssetType.Laptop,
                    "desktops" => AssetType.Desktop,
                    "monitors" => AssetType.Monitor,
                    "printers" => AssetType.Printer,
                    "servers" => AssetType.Router,
                    "mobile-phones" => AssetType.MobilePhone,
                    "peripherals" => AssetType.Keyboard,
                    _ => asset.AssetType
                };
            }
        }

        ApplyTaxonomyDefaults(asset);
        await PopulateFormViewDataAsync(asset);
        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("Id,AssetTag,AssetType,Brand,Model,SerialNumber,Status,Location,Condition,TopLevelCategory,Category,SubCategory,VendorId,Specifications,PurchaseDate,IssueDate,ReturnDate,PreviousOwner,AssetStatus,WarrantyEndDate")] Asset asset)
    {
        if (asset.AssetType == AssetType.Software || string.Equals(asset.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Asset.AssetType), "Use the dedicated Software create forms for software assets.");
        }

        ApplyPhysicalDefaults(asset);

        if (string.IsNullOrWhiteSpace(asset.Location))
        {
            asset.Location = string.Empty;
            ModelState.Remove(nameof(Asset.Location));
        }

        EnsureHardwareCategoryDefault(asset);
        ValidateAndNormalizeTaxonomy(asset);
        await ValidateVendorAsync(asset);

        if (!ModelState.IsValid)
        {
            await PopulateFormViewDataAsync(asset);
            return View(asset);
        }

        asset.CreatedAt = DateTime.UtcNow;

        try
        {
            _context.Add(asset);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Asset.AssetTag), "Asset tag must be unique.");
            await PopulateFormViewDataAsync(asset);
            return View(asset);
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var asset = await _context.Assets.FindAsync(id);
        if (asset is null)
        {
            return NotFound();
        }

        if (TryResolveSoftwareEditAction(asset, out var editAction))
        {
            return RedirectToAction(editAction, new { id = asset.Id });
        }

        ApplyTaxonomyDefaults(asset);
        await PopulateFormViewDataAsync(asset);
        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,AssetTag,AssetType,Brand,Model,SerialNumber,Status,Location,Condition,TopLevelCategory,Category,SubCategory,VendorId,Specifications,PurchaseDate,IssueDate,ReturnDate,WarrantyEndDate")] Asset input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        var previousStatus = asset.Status;
        var existingIsSoftware = asset.AssetType == AssetType.Software
                                 || string.Equals(asset.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase)
                                 || asset.SoftwareCategory.HasValue;

        if (existingIsSoftware || input.AssetType == AssetType.Software || string.Equals(input.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Asset.AssetType), "Use the dedicated Software edit forms for software assets.");
        }

        // Update only fields editable on this form. Keep lifecycle legacy fields (PreviousOwner/AssetStatus) unchanged.
        asset.AssetTag = input.AssetTag;
        asset.AssetType = input.AssetType;
        asset.Brand = input.Brand;
        asset.Model = input.Model;
        asset.SerialNumber = input.SerialNumber;
        asset.Status = input.Status;
        asset.Location = input.Location;
        asset.Condition = input.Condition;
        asset.TopLevelCategory = input.TopLevelCategory;
        asset.Category = input.Category;
        asset.SubCategory = input.SubCategory;
        asset.VendorId = input.VendorId;
        asset.Specifications = input.Specifications;
        asset.PurchaseDate = input.PurchaseDate;
        asset.IssueDate = input.IssueDate;
        asset.ReturnDate = input.ReturnDate;
        asset.WarrantyEndDate = input.WarrantyEndDate;

        ApplyPhysicalDefaults(asset);

        if (asset.Status == AssetStatus.Retired
            && previousStatus != AssetStatus.Retired
            && await HasActiveAssignmentAsync(asset.Id))
        {
            asset.Status = previousStatus;
            ModelState.AddModelError(nameof(Asset.Status), CannotRetireAssignedAssetMessage);
        }

        if (string.IsNullOrWhiteSpace(asset.Location))
        {
            asset.Location = string.Empty;
            ModelState.Remove(nameof(Asset.Location));
        }

        EnsureHardwareCategoryDefault(asset);
        ValidateAndNormalizeTaxonomy(asset);
        await ValidateVendorAsync(asset);

        if (!ModelState.IsValid)
        {
            await PopulateFormViewDataAsync(asset);
            return View(asset);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!AssetExists(asset.Id))
            {
                return NotFound();
            }

            throw;
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Asset.AssetTag), "Asset tag must be unique.");
            await PopulateFormViewDataAsync(asset);
            return View(asset);
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateSoftware()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLicensedApplication()
    {
        var vm = new CreateLicensedApplicationVm
        {
            AssetTag = await GenerateNextSoftwareAssetTagAsync(),
            Status = SoftwareStatus.Active,
            Currency = "KES"
        };

        await PopulateVendorViewDataAsync();
        PopulateLicensedApplicationViewData(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLicensedApplication(CreateLicensedApplicationVm vm)
    {
        NormalizeLicensedApplicationVm(vm);
        EnsureLicensedLifecycleDefaults(vm);
        await ResolveLicensedApplicationPurchaseOrderAsync(
            vm,
            purchaseOrderFieldName: nameof(CreateLicensedApplicationVm.PurchaseOrderId),
            vendorFieldName: nameof(CreateLicensedApplicationVm.VendorId));

        if (string.IsNullOrWhiteSpace(vm.AssetTag))
        {
            vm.AssetTag = await GenerateNextSoftwareAssetTagAsync();
        }

        ValidateLicensedApplicationVm(vm, enforceCatalogRules: true);
        await ValidateVendorAsync(vm.VendorId, nameof(CreateLicensedApplicationVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            PopulateLicensedApplicationViewData(vm);
            return View(vm);
        }

        if (await _context.Assets.AnyAsync(a => a.AssetTag == vm.AssetTag))
        {
            vm.AssetTag = await GenerateNextSoftwareAssetTagAsync();
        }

        var asset = MapFromLicensedApplication(vm);

        try
        {
            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            PopulateLicensedApplicationViewData(vm);
            return View(vm);
        }

        TempData["AssetSuccess"] = "Licensed application created successfully.";
        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [HttpGet("/SoftwareAssets/GetVendorPurchaseOrders")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetVendorPurchaseOrders(int vendorId)
    {
        if (vendorId <= 0)
        {
            return Json(Array.Empty<object>());
        }

        var vendorExists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(v => v.Id == vendorId && !v.IsArchived);

        if (!vendorExists)
        {
            return Json(Array.Empty<object>());
        }

        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.VendorId == vendorId)
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.Id)
            .Select(po => new
            {
                id = po.Id,
                reference = po.PoNumber,
                status = po.Status.ToString(),
                prNumber = po.PurchaseRequest != null ? po.PurchaseRequest.PrNumber : null,
                displayText = po.PurchaseRequest != null
                    ? $"{po.PoNumber} - {po.Status} ({po.PurchaseRequest.PrNumber})"
                    : $"{po.PoNumber} - {po.Status}"
            })
            .ToListAsync();

        return Json(purchaseOrders);
    }

    [HttpGet("/SoftwareAssets/GetPurchaseOrderInvoice")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPurchaseOrderInvoice(int purchaseOrderId)
    {
        if (purchaseOrderId <= 0)
        {
            return BadRequest();
        }

        var purchaseOrder = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => new
            {
                purchaseOrderId = po.Id,
                purchaseOrderReference = po.PoNumber,
                vendorId = po.VendorId,
                invoiceReference = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault(),
                invoiceAmount = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (decimal?)i.Amount)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (purchaseOrder is null)
        {
            return NotFound();
        }

        return Json(purchaseOrder);
    }

    [HttpGet("/Subscriptions/GetPurchaseOrders")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSubscriptionPurchaseOrders(int? vendorId = null, string? q = null)
    {
        var normalizedQ = q?.Trim();

        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => !po.Vendor.IsArchived)
            .AsQueryable();

        if (vendorId.HasValue && vendorId.Value > 0)
        {
            query = query.Where(po => po.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            var lowered = normalizedQ.ToLowerInvariant();
            query = query.Where(po =>
                po.PoNumber.ToLower().Contains(lowered) ||
                po.Vendor.CompanyName.ToLower().Contains(lowered) ||
                (po.PurchaseRequest != null && po.PurchaseRequest.PrNumber.ToLower().Contains(lowered)) ||
                po.Status.ToString().ToLower().Contains(lowered));
        }

        var purchaseOrders = await query
            .OrderByDescending(po => po.OrderDate)
            .ThenByDescending(po => po.Id)
            .Select(po => new
            {
                id = po.Id,
                reference = po.PoNumber,
                vendorName = po.Vendor.CompanyName,
                prNumber = po.PurchaseRequest != null ? po.PurchaseRequest.PrNumber : null,
                status = po.Status.ToString(),
                displayText = po.PurchaseRequest != null
                    ? $"{po.PoNumber} - {po.Vendor.CompanyName} - {po.PurchaseRequest.PrNumber} - {po.Status}"
                    : $"{po.PoNumber} - {po.Vendor.CompanyName} - {po.Status}"
            })
            .ToListAsync();

        return Json(purchaseOrders);
    }

    [HttpGet("/Subscriptions/GetPurchaseOrderFinancials")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSubscriptionPurchaseOrderFinancials(int purchaseOrderId)
    {
        if (purchaseOrderId <= 0)
        {
            return BadRequest();
        }

        var result = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => new
            {
                purchaseOrderId = po.Id,
                purchaseOrderReference = po.PoNumber,
                invoiceReference = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault(),
                cost = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (decimal?)i.Amount)
                    .FirstOrDefault() ?? (decimal?)po.TotalAmount
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            return Json(new
            {
                purchaseOrderId,
                purchaseOrderReference = string.Empty,
                invoiceReference = string.Empty,
                cost = (decimal?)null
            });
        }

        return Json(result);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditLicensedApplication(int id)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (!IsSoftwareSubtype(asset, SoftwareCategory.LicensedApplications))
        {
            return RedirectToSoftwareEdit(asset);
        }

        var vm = new EditLicensedApplicationVm
        {
            Id = asset.Id,
            AssetTag = asset.AssetTag,
            Name = asset.SoftwareName ?? asset.Model,
            Version = asset.SoftwareVersion ?? string.Empty,
            VendorId = asset.VendorId,
            LicenseType = asset.LicenseType,
            LicenseKey = asset.LicenseKey,
            InstalledDate = asset.InstalledDate,
            PurchaseDate = asset.PurchaseDate,
            ExpiryDate = asset.ExpiryDate,
            RenewalReminderDate = asset.RenewalReminderDate,
            Cost = asset.Cost,
            Currency = string.IsNullOrWhiteSpace(asset.Currency) ? "KES" : asset.Currency,
            InvoiceReference = asset.InvoiceReference,
            PurchaseOrderReference = asset.PurchaseOrderReference,
            Status = asset.SoftwareStatus ?? SoftwareStatus.Active,
            Notes = asset.SoftwareNotes
        };

        await PopulateVendorViewDataAsync(vm.VendorId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditLicensedApplication(int id, EditLicensedApplicationVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        NormalizeLicensedApplicationVm(vm);
        EnsureLicensedLifecycleDefaults(vm);
        ValidateLicensedApplicationVm(vm, enforceCatalogRules: false);
        await ValidateVendorAsync(vm.VendorId, nameof(EditLicensedApplicationVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (vm.Status == SoftwareStatus.Retired
            && asset.Status != AssetStatus.Retired
            && await HasActiveAssignmentAsync(asset.Id))
        {
            ModelState.AddModelError(nameof(EditLicensedApplicationVm.Status), CannotRetireAssignedAssetMessage);
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        ApplyLicensedApplicationToAsset(asset, vm);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(EditLicensedApplicationVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOperatingSystem()
    {
        await PopulateVendorViewDataAsync();
        return View(new CreateOperatingSystemVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOperatingSystem(CreateOperatingSystemVm vm)
    {
        await ValidateVendorAsync(vm.VendorId, nameof(CreateOperatingSystemVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = MapFromOperatingSystem(vm);

        try
        {
            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(CreateOperatingSystemVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditOperatingSystem(int id)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (!IsSoftwareSubtype(asset, SoftwareCategory.OperatingSystems))
        {
            return RedirectToSoftwareEdit(asset);
        }

        var vm = new EditOperatingSystemVm
        {
            Id = asset.Id,
            AssetTag = asset.AssetTag,
            Name = asset.SoftwareName ?? asset.Model,
            Edition = asset.Edition ?? string.Empty,
            Version = asset.SoftwareVersion ?? string.Empty,
            BuildNumber = asset.BuildNumber,
            VendorId = asset.VendorId,
            LicenseType = asset.LicenseType,
            LicenseKey = asset.LicenseKey,
            InstalledOn = asset.InstalledOn,
            Location = asset.Location,
            SupportEndDate = asset.SupportEndDate,
            PatchStatus = asset.PatchStatus,
            PurchaseDate = asset.PurchaseDate,
            Status = asset.SoftwareStatus ?? SoftwareStatus.Active,
            Notes = asset.SoftwareNotes
        };

        await PopulateVendorViewDataAsync(vm.VendorId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditOperatingSystem(int id, EditOperatingSystemVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        await ValidateVendorAsync(vm.VendorId, nameof(EditOperatingSystemVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (vm.Status == SoftwareStatus.Retired
            && asset.Status != AssetStatus.Retired
            && await HasActiveAssignmentAsync(asset.Id))
        {
            ModelState.AddModelError(nameof(EditOperatingSystemVm.Status), CannotRetireAssignedAssetMessage);
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        ApplyOperatingSystemToAsset(asset, vm);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(EditOperatingSystemVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSubscription()
    {
        await PopulateVendorViewDataAsync();
        return View(new CreateSubscriptionVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSubscription(CreateSubscriptionVm vm)
    {
        NormalizeSubscriptionVm(vm);
        await ResolveSubscriptionPurchaseOrderAsync(
            vm,
            purchaseOrderFieldName: nameof(CreateSubscriptionVm.PurchaseOrderId),
            vendorFieldName: nameof(CreateSubscriptionVm.VendorId));

        ValidateSubscriptionVm(vm);
        await ValidateVendorAsync(vm.VendorId, nameof(CreateSubscriptionVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = MapFromSubscription(vm);

        try
        {
            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(CreateSubscriptionVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSubscription(int id)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (!IsSoftwareSubtype(asset, SoftwareCategory.Subscriptions))
        {
            return RedirectToSoftwareEdit(asset);
        }

        var vm = new EditSubscriptionVm
        {
            Id = asset.Id,
            AssetTag = asset.AssetTag,
            Name = asset.SoftwareName ?? asset.Model,
            PlanOrTier = asset.PlanOrTier,
            VendorId = asset.VendorId,
            BillingCycle = asset.BillingCycle ?? SoftwareBillingCycle.Monthly,
            StartDate = asset.StartDate,
            ExpiryDate = asset.ExpiryDate,
            AutoRenew = asset.AutoRenew,
            RenewalReminderDate = asset.RenewalReminderDate,
            Cost = asset.Cost,
            Currency = string.IsNullOrWhiteSpace(asset.Currency) ? "KES" : asset.Currency,
            InvoiceReference = asset.InvoiceReference,
            PurchaseOrderReference = asset.PurchaseOrderReference,
            AssignedToUserOrDepartment = asset.AssignedToUserOrDepartment,
            InstalledOn = asset.InstalledOn,
            Location = asset.Location,
            Status = asset.SoftwareStatus ?? SoftwareStatus.Active,
            Notes = asset.SoftwareNotes
        };

        await PopulateVendorViewDataAsync(vm.VendorId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSubscription(int id, EditSubscriptionVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        ValidateSubscriptionVm(vm);
        await ValidateVendorAsync(vm.VendorId, nameof(EditSubscriptionVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (vm.Status == SoftwareStatus.Retired
            && asset.Status != AssetStatus.Retired
            && await HasActiveAssignmentAsync(asset.Id))
        {
            ModelState.AddModelError(nameof(EditSubscriptionVm.Status), CannotRetireAssignedAssetMessage);
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        ApplySubscriptionToAsset(asset, vm);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(EditSubscriptionVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOpenSource()
    {
        await PopulateVendorViewDataAsync();
        return View(new CreateOpenSourceVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOpenSource(CreateOpenSourceVm vm)
    {
        await ValidateVendorAsync(vm.VendorId, nameof(CreateOpenSourceVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = MapFromOpenSource(vm);

        try
        {
            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(CreateOpenSourceVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditOpenSource(int id)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (!IsSoftwareSubtype(asset, SoftwareCategory.OpenSource))
        {
            return RedirectToSoftwareEdit(asset);
        }

        var vm = new EditOpenSourceVm
        {
            Id = asset.Id,
            AssetTag = asset.AssetTag,
            Name = asset.SoftwareName ?? asset.Model,
            Version = asset.SoftwareVersion,
            OpenSourceLicenseType = asset.OpenSourceLicenseType,
            MaintainerOrVendor = asset.MaintainerOrVendor,
            VendorId = asset.VendorId,
            InstalledOn = asset.InstalledOn,
            Location = asset.Location,
            DeploymentEnvironment = asset.DeploymentEnvironment,
            SecurityStatus = asset.SecurityStatus,
            LastUpdatedDate = asset.LastUpdatedDate,
            Status = asset.SoftwareStatus ?? SoftwareStatus.Active,
            Notes = asset.SoftwareNotes
        };

        await PopulateVendorViewDataAsync(vm.VendorId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditOpenSource(int id, EditOpenSourceVm vm)
    {
        if (id != vm.Id)
        {
            return NotFound();
        }

        await ValidateVendorAsync(vm.VendorId, nameof(EditOpenSourceVm.VendorId));

        if (!ModelState.IsValid)
        {
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        if (vm.Status == SoftwareStatus.Retired
            && asset.Status != AssetStatus.Retired
            && await HasActiveAssignmentAsync(asset.Id))
        {
            ModelState.AddModelError(nameof(EditOpenSourceVm.Status), CannotRetireAssignedAssetMessage);
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        ApplyOpenSourceToAsset(asset, vm);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(EditOpenSourceVm.AssetTag), "Asset tag must be unique.");
            await PopulateVendorViewDataAsync(vm.VendorId);
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var asset = await _context.Assets.FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null)
        {
            return NotFound();
        }

        var isRetired = asset.Status == AssetStatus.Retired
                        || asset.SoftwareStatus == SoftwareStatus.Retired;
        if (!isRetired)
        {
            TempData["AssetError"] = "Only retired assets can be permanently deleted. Retire the asset first.";
            return RedirectToAction(nameof(Details), new { id = asset.Id });
        }

        ViewData["AssignmentsCount"] = await _context.Assignments.AsNoTracking().CountAsync(a => a.AssetId == asset.Id);
        ViewData["IssuesCount"] = await _context.Issues.AsNoTracking().CountAsync(i => i.AssetId == asset.Id);
        ViewData["SupportTicketsCount"] = await _context.VendorSupportTickets.AsNoTracking().CountAsync(t => t.AssetId == asset.Id);

        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Retire(int id, string? returnUrl = null)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id);
        if (asset is null)
        {
            TempData["AssetError"] = "Asset not found.";
            return RedirectToAction(nameof(Index));
        }

        var alreadyRetired = asset.Status == AssetStatus.Retired
                             || asset.SoftwareStatus == SoftwareStatus.Retired;
        if (alreadyRetired)
        {
            TempData["AssetError"] = "Asset is already retired.";
        }
        else if (await HasActiveAssignmentAsync(asset.Id))
        {
            TempData["AssetError"] = CannotRetireAssignedAssetMessage;
        }
        else
        {
            asset.Status = AssetStatus.Retired;
            if (IsSoftwareAsset(asset))
            {
                asset.SoftwareStatus = SoftwareStatus.Retired;
            }

            asset.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["AssetSuccess"] = "Asset retired successfully.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var asset = await _context.Assets.FindAsync(id);
        if (asset is null)
        {
            TempData["AssetError"] = "Asset not found.";
            return RedirectToAction(nameof(Index));
        }

        var isRetired = asset.Status == AssetStatus.Retired
                        || asset.SoftwareStatus == SoftwareStatus.Retired;
        if (!isRetired)
        {
            TempData["AssetError"] = "Only retired assets can be permanently deleted. Retire the asset first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var linkedAssignments = await _context.Assignments
                .Where(a => a.AssetId == id)
                .ToListAsync();

            var linkedIssues = await _context.Issues
                .Where(i => i.AssetId == id)
                .ToListAsync();

            var linkedSupportTickets = await _context.VendorSupportTickets
                .Where(t => t.AssetId == id)
                .ToListAsync();

            await using var tx = await _context.Database.BeginTransactionAsync();

            if (linkedAssignments.Count > 0)
            {
                _context.Assignments.RemoveRange(linkedAssignments);
            }

            if (linkedIssues.Count > 0)
            {
                foreach (var issue in linkedIssues)
                {
                    issue.AssetId = null;
                }
            }

            if (linkedSupportTickets.Count > 0)
            {
                foreach (var ticket in linkedSupportTickets)
                {
                    ticket.AssetId = null;
                }
            }

            await _context.SaveChangesAsync();

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
            TempData["AssetSuccess"] = "Asset deleted permanently with related history.";
        }
        catch (DbUpdateException)
        {
            TempData["AssetError"] = "Unable to delete this asset right now. Please retry, or contact support if the issue persists.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    private bool AssetExists(int id)
    {
        return _context.Assets.Any(e => e.Id == id);
    }

    private async Task PopulateFormViewDataAsync(Asset asset)
    {
        var isHardwareContext = IsHardwareSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory)
            || string.Equals(Request.Query["group"], "hardware", StringComparison.OrdinalIgnoreCase);
        var isPhysicalContext = IsPhysicalSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory)
            || string.Equals(Request.Query["group"], PhysicalTopLevelKey, StringComparison.OrdinalIgnoreCase);

        if (isHardwareContext)
        {
            if (string.IsNullOrWhiteSpace(asset.TopLevelCategory))
            {
                asset.TopLevelCategory = "hardware";
            }

            EnsureHardwareCategoryDefault(asset);
        }
        else if (isPhysicalContext && string.IsNullOrWhiteSpace(asset.TopLevelCategory))
        {
            asset.TopLevelCategory = PhysicalTopLevelKey;
        }

        var nonSoftwareOptions = _assetTaxonomyService.GetOptionsTree()
            .Where(x => !string.Equals(x.Key, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ViewData["AssetTaxonomyJson"] = JsonSerializer.Serialize(nonSoftwareOptions);
        ViewData["SelectedTopLevelCategory"] = asset.TopLevelCategory ?? string.Empty;
        ViewData["SelectedCategory"] = asset.Category ?? string.Empty;
        ViewData["SelectedSubCategory"] = asset.SubCategory ?? string.Empty;
        ViewData["HideTaxonomySelectors"] = isHardwareContext || isPhysicalContext;
        ViewData["IsPhysicalContext"] = isPhysicalContext;
        ViewData["HardwareCategoryLabel"] = ToReadableLabel(asset.Category);

        var hasActiveAssignment = asset.Id > 0 && await HasActiveAssignmentAsync(asset.Id);
        ViewData["HasActiveAssignment"] = hasActiveAssignment;
        ViewData["AssetStatusOptions"] = new SelectList(
            Enum.GetValues<AssetStatus>()
                .Where(s => !hasActiveAssignment || s != AssetStatus.Retired || asset.Status == AssetStatus.Retired)
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.ToString()
                }),
            "Value",
            "Text",
            asset.Status.ToString());

        if (isPhysicalContext)
        {
            ViewData["PhysicalLegacyAssetType"] = IsPhysicalAssetType(asset.AssetType)
                ? null
                : asset.AssetType.ToString();
        }
        else
        {
            ViewData["PhysicalLegacyAssetType"] = null;
        }

        var nonSoftwareAssetTypes = Enum.GetValues<AssetType>()
            .Where(t => t != AssetType.Software && !IsPhysicalAssetType(t))
            .Select(t => new SelectListItem { Text = GetAssetTypeDisplayName(t), Value = t.ToString() })
            .ToList();

        ViewData["NonSoftwareAssetTypes"] = nonSoftwareAssetTypes;

        await PopulateVendorViewDataAsync(asset.VendorId);
    }

    private async Task<bool> HasActiveAssignmentAsync(int assetId)
    {
        return await _context.Assignments
            .AsNoTracking()
            .AnyAsync(a => a.AssetId == assetId &&
                           (a.Status == AssignmentStatus.Active
                            || a.Status == AssignmentStatus.PendingAcceptance
                            || a.Status == AssignmentStatus.Accepted
                            || a.Status == AssignmentStatus.ReturnRequested
                            || (a.ReturnedApprovedAt == null
                                && a.Status != AssignmentStatus.Returned
                                && a.Status != AssignmentStatus.ReturnedApproved
                                && a.Status != AssignmentStatus.Reverted
                                && a.Status != AssignmentStatus.Rejected)));
    }

    private async Task PopulateVendorViewDataAsync(int? selectedVendorId = null)
    {
        var vendors = await _context.Vendors
            .AsNoTracking()
            .Where(v => !v.IsArchived)
            .OrderBy(v => v.CompanyName)
            .Select(v => new
            {
                v.Id,
                Label = $"{v.CompanyName} ({v.VendorCode})"
            })
            .ToListAsync();

        ViewData["VendorId"] = new SelectList(vendors, "Id", "Label", selectedVendorId);
    }

    private void PopulateLicensedApplicationViewData(CreateLicensedApplicationVm vm)
    {
        ViewData["LicensedStatusOptions"] = new SelectList(
            CreateLicensedApplicationVm.AllowedStatuses.Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString()
            }),
            "Value",
            "Text",
            vm.Status.ToString());

        ViewData["LicensedTypeOptions"] = new SelectList(
            CreateLicensedApplicationVm.AllowedLicenseTypes.Select(t => new SelectListItem
            {
                Value = t,
                Text = t
            }),
            "Value",
            "Text",
            vm.LicenseType);

        ViewData["LicensedCurrencyOptions"] = new SelectList(
            CreateLicensedApplicationVm.AllowedCurrencies.Select(c => new SelectListItem
            {
                Value = c,
                Text = c
            }),
            "Value",
            "Text",
            vm.Currency);
    }

    private async Task<string> GenerateNextSoftwareAssetTagAsync()
    {
        var tags = await _context.Assets
            .AsNoTracking()
            .Where(a => a.AssetTag.StartsWith("SW-"))
            .Select(a => a.AssetTag)
            .ToListAsync();

        var max = 0;
        foreach (var tag in tags)
        {
            if (tag.Length <= 3)
            {
                continue;
            }

            if (int.TryParse(tag[3..], out var parsed) && parsed > max)
            {
                max = parsed;
            }
        }

        return $"SW-{(max + 1):D5}";
    }

    private void ValidateAndNormalizeTaxonomy(Asset asset)
    {
        ApplyTaxonomyDefaults(asset);

        if (!_assetTaxonomyService.TryNormalizeSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory, out var normalized, out var validationError))
        {
            ModelState.AddModelError(nameof(Asset.Category), validationError ?? "Invalid asset category selection.");
            return;
        }

        asset.TopLevelCategory = normalized.TopLevelCategory;
        asset.Category = normalized.Category;
        asset.SubCategory = normalized.SubCategory;
    }

    private void ApplyTaxonomyDefaults(Asset asset)
    {
        var hasAnySelection = !string.IsNullOrWhiteSpace(asset.TopLevelCategory)
            || !string.IsNullOrWhiteSpace(asset.Category)
            || !string.IsNullOrWhiteSpace(asset.SubCategory);

        if (hasAnySelection)
        {
            if (_assetTaxonomyService.TryNormalizeSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory, out var normalized, out _)
                && !normalized.IsEmpty)
            {
                asset.TopLevelCategory = normalized.TopLevelCategory;
                asset.Category = normalized.Category;
                asset.SubCategory = normalized.SubCategory;
            }

            return;
        }

        var inferred = _assetTaxonomyService.InferSelectionFromAssetType(asset.AssetType);
        asset.TopLevelCategory = inferred.TopLevelCategory;
        asset.Category = inferred.Category;
        asset.SubCategory = inferred.SubCategory;
    }

    private async Task ValidateVendorAsync(Asset asset)
    {
        await ValidateVendorAsync(asset.VendorId, nameof(Asset.VendorId));
    }

    private async Task ValidateVendorAsync(int? vendorId, string fieldName)
    {
        if (!vendorId.HasValue)
        {
            return;
        }

        var vendorExists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(v => v.Id == vendorId.Value && !v.IsArchived);

        if (!vendorExists)
        {
            ModelState.AddModelError(fieldName, "Selected vendor does not exist.");
        }
    }

    private static bool IsSoftwareSelection(AssetTaxonomySelection selection)
    {
        return string.Equals(selection.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHardwareSelection(AssetTaxonomySelection selection)
    {
        return IsHardwareSelection(selection.TopLevelCategory, selection.Category, selection.SubCategory);
    }

    private static bool IsHardwareSelection(string? topLevelCategory, string? category, string? subCategory)
    {
        return string.Equals(topLevelCategory, "hardware", StringComparison.OrdinalIgnoreCase)
               && string.IsNullOrWhiteSpace(subCategory);
    }

    private static bool IsPhysicalSelection(string? topLevelCategory, string? category, string? subCategory)
    {
        return string.Equals(topLevelCategory, PhysicalTopLevelKey, StringComparison.OrdinalIgnoreCase)
               || string.Equals(category, "furniture", StringComparison.OrdinalIgnoreCase)
               || string.Equals(category, "fixtures", StringComparison.OrdinalIgnoreCase)
               || string.Equals(category, "vehicles", StringComparison.OrdinalIgnoreCase)
               || string.Equals(subCategory, "desks", StringComparison.OrdinalIgnoreCase)
               || string.Equals(subCategory, "chairs", StringComparison.OrdinalIgnoreCase)
               || string.Equals(subCategory, "cabinets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPhysicalAssetType(AssetType type)
    {
        return type is AssetType.Chairs
            or AssetType.Whiteboards
            or AssetType.Desks
            or AssetType.Cabinets
            or AssetType.Tables
            or AssetType.Drawers
            or AssetType.Other;
    }

    private void ApplyPhysicalDefaults(Asset asset)
    {
        if (!IsPhysicalSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(asset.Brand))
        {
            asset.Brand = "N/A";
            ModelState.Remove(nameof(Asset.Brand));
        }
        else
        {
            asset.Brand = asset.Brand.Trim();
        }

        if (string.IsNullOrWhiteSpace(asset.Model))
        {
            asset.Model = asset.AssetType.ToString();
            ModelState.Remove(nameof(Asset.Model));
        }
        else
        {
            asset.Model = asset.Model.Trim();
        }

        if (string.IsNullOrWhiteSpace(asset.Condition))
        {
            asset.Condition = "N/A";
            ModelState.Remove(nameof(Asset.Condition));
        }
        else
        {
            asset.Condition = asset.Condition.Trim();
        }

        asset.SerialNumber = string.IsNullOrWhiteSpace(asset.SerialNumber)
            ? string.Empty
            : asset.SerialNumber.Trim();
    }

    private void EnsureHardwareCategoryDefault(Asset asset)
    {
        if (!string.Equals(asset.TopLevelCategory, "hardware", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (asset.AssetType == AssetType.MobilePhone)
        {
            asset.Category = "mobile-phones";
            asset.SubCategory = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(asset.Category))
        {
            return;
        }

        var inferred = _assetTaxonomyService.InferSelectionFromAssetType(asset.AssetType);
        if (string.Equals(inferred.TopLevelCategory, "hardware", StringComparison.OrdinalIgnoreCase))
        {
            asset.Category = inferred.Category;
            if (string.IsNullOrWhiteSpace(asset.SubCategory))
            {
                asset.SubCategory = inferred.SubCategory;
            }
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RepairMobilePhoneCategorization()
    {
        var wrongMobilePhones = await _context.Assets
            .Where(a => a.AssetType == AssetType.MobilePhone
                        && ((a.TopLevelCategory == null || a.TopLevelCategory == string.Empty || a.TopLevelCategory != "hardware")
                            || a.Category != "mobile-phones"
                            || (a.SubCategory != null && a.SubCategory != string.Empty)))
            .ToListAsync();

        if (wrongMobilePhones.Count == 0)
        {
            TempData["AssetSuccess"] = "No Mobile Phone categorization records needed correction.";
            return RedirectToAction(nameof(Index), new { group = "hardware", category = "mobile-phones" });
        }

        var now = DateTime.UtcNow;
        foreach (var asset in wrongMobilePhones)
        {
            asset.TopLevelCategory = "hardware";
            asset.Category = "mobile-phones";
            asset.SubCategory = null;
            asset.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();
        TempData["AssetSuccess"] = $"Repaired {wrongMobilePhones.Count} Mobile Phone asset categorization record(s).";
        return RedirectToAction(nameof(Index), new { group = "hardware", category = "mobile-phones" });
    }

    private static (string? Group, string? Category, string? SubCategory) NormalizeCreateSelection(
        string? group,
        string? category,
        string? subCategory)
    {
        // Support /Assets/Create?group=Hardware&subCategory=Laptops by mapping hardware leaf to Category.
        if (string.Equals(group, "hardware", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(category)
            && !string.IsNullOrWhiteSpace(subCategory))
        {
            return (group, subCategory, null);
        }

        return (group, category, subCategory);
    }

    private static string? ToReadableLabel(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return string.Join(' ', key.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string GetAssetTypeDisplayName(AssetType type)
    {
        if (type == AssetType.MobilePhone)
        {
            return "Mobile Phones";
        }

        var member = typeof(AssetType).GetMember(type.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .OfType<DisplayAttribute>()
            .FirstOrDefault()
            ?.Name;

        return string.IsNullOrWhiteSpace(display) ? type.ToString() : display;
    }

    private static bool TryParseAssetTypeFilter(string? type, out AssetType assetType)
    {
        assetType = default;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        var trimmed = type.Trim();
        if (Enum.TryParse<AssetType>(trimmed, true, out assetType))
        {
            return true;
        }

        var compact = trimmed.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<AssetType>(compact, true, out assetType);
    }

    private IActionResult RedirectToSoftwareCreate(string? categoryKey)
    {
        var action = ResolveSoftwareCreateAction(categoryKey);
        if (string.IsNullOrWhiteSpace(action))
        {
            return RedirectToAction(nameof(CreateSoftware));
        }

        return RedirectToAction(action);
    }

    private IActionResult RedirectToSoftwareEdit(Asset asset)
    {
        if (TryResolveSoftwareEditAction(asset, out var editAction))
        {
            return RedirectToAction(editAction, new { id = asset.Id });
        }

        return RedirectToAction(nameof(Details), new { id = asset.Id });
    }

    private static string? ResolveSoftwareCreateAction(string? categoryKey)
    {
        return categoryKey?.ToLowerInvariant() switch
        {
            "licensed-applications" => nameof(CreateLicensedApplication),
            "operating-systems" => nameof(CreateOperatingSystem),
            "subscriptions" => nameof(CreateSubscription),
            "open-source" => nameof(CreateOpenSource),
            _ => null
        };
    }

    private static string? ResolveSoftwareEditAction(SoftwareCategory category)
    {
        return category switch
        {
            SoftwareCategory.LicensedApplications => nameof(EditLicensedApplication),
            SoftwareCategory.OperatingSystems => nameof(EditOperatingSystem),
            SoftwareCategory.Subscriptions => nameof(EditSubscription),
            SoftwareCategory.OpenSource => nameof(EditOpenSource),
            _ => null
        };
    }

    private static string ResolveSoftwareCategoryKey(SoftwareCategory category)
    {
        return category switch
        {
            SoftwareCategory.LicensedApplications => "licensed-applications",
            SoftwareCategory.OperatingSystems => "operating-systems",
            SoftwareCategory.Subscriptions => "subscriptions",
            SoftwareCategory.OpenSource => "open-source",
            _ => "licensed-applications"
        };
    }

    private static bool TryResolveSoftwareEditAction(Asset asset, out string? editAction)
    {
        editAction = null;
        if (!IsSoftwareAsset(asset))
        {
            return false;
        }

        var softwareCategory = ResolveSoftwareCategory(asset);
        if (!softwareCategory.HasValue)
        {
            return false;
        }

        editAction = ResolveSoftwareEditAction(softwareCategory.Value);
        return !string.IsNullOrWhiteSpace(editAction);
    }

    private static SoftwareCategory? ResolveSoftwareCategory(Asset asset)
    {
        if (asset.SoftwareCategory.HasValue)
        {
            return asset.SoftwareCategory;
        }

        return asset.Category?.ToLowerInvariant() switch
        {
            "licensed-applications" => SoftwareCategory.LicensedApplications,
            "operating-systems" => SoftwareCategory.OperatingSystems,
            "subscriptions" => SoftwareCategory.Subscriptions,
            "open-source" => SoftwareCategory.OpenSource,
            _ => null
        };
    }

    private static bool IsSoftwareAsset(Asset asset)
    {
        return asset.AssetType == AssetType.Software
               || asset.SoftwareCategory.HasValue
               || string.Equals(asset.TopLevelCategory, SoftwareTopLevelKey, StringComparison.OrdinalIgnoreCase)
               || string.Equals(asset.Category, "licensed-applications", StringComparison.OrdinalIgnoreCase)
               || string.Equals(asset.Category, "operating-systems", StringComparison.OrdinalIgnoreCase)
               || string.Equals(asset.Category, "subscriptions", StringComparison.OrdinalIgnoreCase)
               || string.Equals(asset.Category, "open-source", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSoftwareSubtype(Asset asset, SoftwareCategory expected)
    {
        return ResolveSoftwareCategory(asset) == expected;
    }

    private static AssetStatus MapAssetStatus(SoftwareStatus softwareStatus)
    {
        return softwareStatus switch
        {
            SoftwareStatus.Retired => AssetStatus.Retired,
            _ => AssetStatus.InStock
        };
    }

    private void ValidateLicensedApplicationVm(CreateLicensedApplicationVm vm, bool enforceCatalogRules)
    {
        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Name), "Name is required.");
        }

        if (vm.InstalledDate.HasValue && vm.InstalledDate.Value.Date > DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.InstalledDate), "Installed Date cannot be later than today.");
        }

        if (enforceCatalogRules)
        {
            if (!CreateLicensedApplicationVm.AllowedStatuses.Contains(vm.Status))
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Status), "Selected status is not allowed for licensed applications.");
            }

            if (string.IsNullOrWhiteSpace(vm.LicenseType))
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.LicenseType), "License Type is required.");
            }
            else if (!CreateLicensedApplicationVm.AllowedLicenseTypes.Contains(vm.LicenseType, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.LicenseType), "Select a valid License Type.");
            }

            if (string.IsNullOrWhiteSpace(vm.Currency))
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Currency), "Currency is required.");
            }
            else if (!CreateLicensedApplicationVm.AllowedCurrencies.Contains(vm.Currency, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Currency), "Select a valid currency.");
            }
        }

        if (vm.Cost.HasValue)
        {
            if (vm.Cost.Value < 0)
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Cost), "Cost must be greater than or equal to zero.");
            }
            else if (decimal.Round(vm.Cost.Value, 2) != vm.Cost.Value)
            {
                ModelState.AddModelError(nameof(CreateLicensedApplicationVm.Cost), "Cost can only have up to 2 decimal places.");
            }
        }

        if (vm.PurchaseDate.HasValue && vm.PurchaseDate.Value.Date > DateTime.UtcNow.Date.AddYears(2))
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.PurchaseDate), "Purchase Date is too far in the future.");
        }

        var requiresExpiry = string.Equals(vm.LicenseType, "Subscription", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(vm.LicenseType, "Trial", StringComparison.OrdinalIgnoreCase);
        if (requiresExpiry && !vm.ExpiryDate.HasValue)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.ExpiryDate), "Expiry Date is required for Subscription and Trial licenses.");
        }

        if (vm.ExpiryDate.HasValue && vm.PurchaseDate.HasValue && vm.ExpiryDate.Value.Date < vm.PurchaseDate.Value.Date)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.ExpiryDate), "Expiry Date cannot be before Purchase Date.");
        }

        if (vm.InstalledDate.HasValue && vm.ExpiryDate.HasValue && vm.InstalledDate.Value.Date > vm.ExpiryDate.Value.Date)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.InstalledDate), "Installed Date cannot be later than Expiry Date.");
        }

        if (vm.RenewalReminderDate.HasValue && vm.ExpiryDate.HasValue && vm.RenewalReminderDate.Value.Date > vm.ExpiryDate.Value.Date)
        {
            ModelState.AddModelError(nameof(CreateLicensedApplicationVm.RenewalReminderDate), "Renewal Reminder Date must be on or before Expiry Date.");
        }
    }

    private static void EnsureLicensedLifecycleDefaults(CreateLicensedApplicationVm vm)
    {
        if (vm.ExpiryDate.HasValue && !vm.RenewalReminderDate.HasValue)
        {
            vm.RenewalReminderDate = vm.ExpiryDate.Value.Date.AddDays(-30);
        }
    }

    private static void NormalizeLicensedApplicationVm(CreateLicensedApplicationVm vm)
    {
        vm.AssetTag = vm.AssetTag?.Trim() ?? string.Empty;
        vm.Name = vm.Name?.Trim() ?? string.Empty;
        vm.Version = NullIfWhiteSpace(vm.Version);
        vm.LicenseType = NullIfWhiteSpace(vm.LicenseType);
        vm.LicenseKey = NullIfWhiteSpace(vm.LicenseKey);
        vm.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim().ToUpperInvariant();
        vm.InvoiceReference = NullIfWhiteSpace(vm.InvoiceReference);
        vm.PurchaseOrderReference = NullIfWhiteSpace(vm.PurchaseOrderReference);
        vm.Notes = NullIfWhiteSpace(vm.Notes);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task ResolveLicensedApplicationPurchaseOrderAsync(
        CreateLicensedApplicationVm vm,
        string purchaseOrderFieldName,
        string vendorFieldName)
    {
        if (!vm.PurchaseOrderId.HasValue)
        {
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        if (!vm.VendorId.HasValue)
        {
            ModelState.AddModelError(purchaseOrderFieldName, "Select a vendor before selecting a purchase order.");
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        var purchaseOrder = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == vm.PurchaseOrderId.Value)
            .Select(po => new
            {
                po.Id,
                po.VendorId,
                po.PoNumber,
                LatestInvoiceReference = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault(),
                LatestInvoiceAmount = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (decimal?)i.Amount)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (purchaseOrder is null)
        {
            ModelState.AddModelError(purchaseOrderFieldName, "Selected purchase order does not exist.");
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        if (purchaseOrder.VendorId != vm.VendorId.Value)
        {
            ModelState.AddModelError(vendorFieldName, "Selected vendor does not match the selected purchase order.");
            ModelState.AddModelError(purchaseOrderFieldName, "The selected purchase order does not belong to the selected vendor.");
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        vm.PurchaseOrderReference = purchaseOrder.PoNumber;
        vm.InvoiceReference = NullIfWhiteSpace(purchaseOrder.LatestInvoiceReference);
        if (purchaseOrder.LatestInvoiceAmount.HasValue)
        {
            vm.Cost = purchaseOrder.LatestInvoiceAmount.Value;
        }
    }

    private static void NormalizeSubscriptionVm(CreateSubscriptionVm vm)
    {
        vm.AssetTag = vm.AssetTag?.Trim() ?? string.Empty;
        vm.Name = vm.Name?.Trim() ?? string.Empty;
        vm.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim().ToUpperInvariant();
        vm.InvoiceReference = NullIfWhiteSpace(vm.InvoiceReference);
        vm.PurchaseOrderReference = NullIfWhiteSpace(vm.PurchaseOrderReference);
        vm.AssignedToUserOrDepartment = NullIfWhiteSpace(vm.AssignedToUserOrDepartment);
        vm.InstalledOn = NullIfWhiteSpace(vm.InstalledOn);
        vm.Location = NullIfWhiteSpace(vm.Location);
        vm.Notes = NullIfWhiteSpace(vm.Notes);
    }

    private async Task ResolveSubscriptionPurchaseOrderAsync(
        CreateSubscriptionVm vm,
        string purchaseOrderFieldName,
        string vendorFieldName)
    {
        if (!vm.PurchaseOrderId.HasValue)
        {
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        var purchaseOrder = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == vm.PurchaseOrderId.Value)
            .Select(po => new
            {
                po.Id,
                po.VendorId,
                po.PoNumber,
                po.TotalAmount,
                LatestInvoiceReference = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault(),
                LatestInvoiceAmount = po.Invoices
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Select(i => (decimal?)i.Amount)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (purchaseOrder is null)
        {
            ModelState.AddModelError(purchaseOrderFieldName, "Selected purchase order does not exist.");
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        if (vm.VendorId.HasValue && purchaseOrder.VendorId != vm.VendorId.Value)
        {
            ModelState.AddModelError(vendorFieldName, "Selected vendor does not match the selected purchase order.");
            ModelState.AddModelError(purchaseOrderFieldName, "The selected purchase order does not belong to the selected vendor.");
            vm.PurchaseOrderReference = null;
            vm.InvoiceReference = null;
            return;
        }

        if (!vm.VendorId.HasValue)
        {
            vm.VendorId = purchaseOrder.VendorId;
        }

        vm.PurchaseOrderReference = purchaseOrder.PoNumber;
        if (string.IsNullOrWhiteSpace(vm.InvoiceReference))
        {
            vm.InvoiceReference = NullIfWhiteSpace(purchaseOrder.LatestInvoiceReference);
        }

        if (!vm.Cost.HasValue)
        {
            vm.Cost = purchaseOrder.LatestInvoiceAmount ?? purchaseOrder.TotalAmount;
        }
    }

    private void ValidateSubscriptionVm(CreateSubscriptionVm vm)
    {
        if (vm.ExpiryDate.HasValue && vm.StartDate.HasValue && vm.ExpiryDate.Value.Date < vm.StartDate.Value.Date)
        {
            ModelState.AddModelError(nameof(CreateSubscriptionVm.ExpiryDate), "Expiry Date cannot be before Start Date.");
        }

        if (vm.RenewalReminderDate.HasValue && vm.ExpiryDate.HasValue && vm.RenewalReminderDate.Value.Date > vm.ExpiryDate.Value.Date)
        {
            ModelState.AddModelError(nameof(CreateSubscriptionVm.RenewalReminderDate), "Renewal Reminder Date must be on or before Expiry Date.");
        }
    }

    private Asset MapFromLicensedApplication(CreateLicensedApplicationVm vm)
    {
        var asset = BuildSoftwareAssetBase(vm.AssetTag, vm.Name, null, vm.VendorId, SoftwareCategory.LicensedApplications, vm.Status);
        ApplyLicensedApplicationToAsset(asset, vm);
        asset.CreatedAt = DateTime.UtcNow;
        return asset;
    }

    private Asset MapFromOperatingSystem(CreateOperatingSystemVm vm)
    {
        var asset = BuildSoftwareAssetBase(vm.AssetTag, vm.Name, vm.Location, vm.VendorId, SoftwareCategory.OperatingSystems, vm.Status);
        ApplyOperatingSystemToAsset(asset, vm);
        asset.CreatedAt = DateTime.UtcNow;
        return asset;
    }

    private Asset MapFromSubscription(CreateSubscriptionVm vm)
    {
        var asset = BuildSoftwareAssetBase(vm.AssetTag, vm.Name, vm.Location, vm.VendorId, SoftwareCategory.Subscriptions, vm.Status);
        ApplySubscriptionToAsset(asset, vm);
        asset.CreatedAt = DateTime.UtcNow;
        return asset;
    }

    private Asset MapFromOpenSource(CreateOpenSourceVm vm)
    {
        var asset = BuildSoftwareAssetBase(vm.AssetTag, vm.Name, vm.Location, vm.VendorId, SoftwareCategory.OpenSource, vm.Status);
        ApplyOpenSourceToAsset(asset, vm);
        asset.CreatedAt = DateTime.UtcNow;
        return asset;
    }

    private static Asset BuildSoftwareAssetBase(string assetTag, string name, string? location, int? vendorId, SoftwareCategory softwareCategory, SoftwareStatus softwareStatus)
    {
        var now = DateTime.UtcNow;

        return new Asset
        {
            AssetTag = assetTag.Trim(),
            AssetType = AssetType.Software,
            Brand = "Software",
            Model = name.Trim(),
            SerialNumber = string.Empty,
            Status = MapAssetStatus(softwareStatus),
            Location = string.IsNullOrWhiteSpace(location) ? "Not specified" : location.Trim(),
            Condition = "N/A",
            TopLevelCategory = SoftwareTopLevelKey,
            Category = ResolveSoftwareCategoryKey(softwareCategory),
            SubCategory = null,
            VendorId = vendorId,
            SoftwareCategory = softwareCategory,
            SoftwareName = name.Trim(),
            SoftwareStatus = softwareStatus,
            Currency = "KES",
            UpdatedAt = now,
            CreatedAt = now
        };
    }

    private static void ApplyLicensedApplicationToAsset(Asset asset, CreateLicensedApplicationVm vm)
    {
        asset.AssetTag = vm.AssetTag.Trim();
        asset.AssetType = AssetType.Software;
        asset.Brand = "Software";
        asset.Model = vm.Name.Trim();
        asset.Status = MapAssetStatus(vm.Status);
        asset.Condition = "N/A";
        asset.TopLevelCategory = SoftwareTopLevelKey;
        asset.Category = ResolveSoftwareCategoryKey(SoftwareCategory.LicensedApplications);
        asset.SubCategory = null;
        asset.VendorId = vm.VendorId;
        asset.SoftwareCategory = SoftwareCategory.LicensedApplications;
        asset.SoftwareName = vm.Name.Trim();
        asset.SoftwareVersion = vm.Version?.Trim();
        asset.LicenseType = vm.LicenseType?.Trim();
        asset.LicenseKey = vm.LicenseKey?.Trim();
        asset.InstalledDate = vm.InstalledDate;
        asset.PurchaseDate = vm.PurchaseDate;
        asset.ExpiryDate = vm.ExpiryDate;
        asset.RenewalReminderDate = vm.RenewalReminderDate;
        asset.Cost = vm.Cost;
        asset.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim();
        asset.InvoiceReference = vm.InvoiceReference?.Trim();
        asset.PurchaseOrderReference = vm.PurchaseOrderReference?.Trim();
        asset.SoftwareStatus = vm.Status;
        asset.SoftwareNotes = vm.Notes?.Trim();
        asset.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyOperatingSystemToAsset(Asset asset, CreateOperatingSystemVm vm)
    {
        asset.AssetTag = vm.AssetTag.Trim();
        asset.AssetType = AssetType.Software;
        asset.Brand = "Software";
        asset.Model = vm.Name.Trim();
        asset.Status = MapAssetStatus(vm.Status);
        asset.Location = string.IsNullOrWhiteSpace(vm.Location) ? "Not specified" : vm.Location.Trim();
        asset.Condition = "N/A";
        asset.TopLevelCategory = SoftwareTopLevelKey;
        asset.Category = ResolveSoftwareCategoryKey(SoftwareCategory.OperatingSystems);
        asset.SubCategory = null;
        asset.VendorId = vm.VendorId;
        asset.SoftwareCategory = SoftwareCategory.OperatingSystems;
        asset.SoftwareName = vm.Name.Trim();
        asset.SoftwareVersion = vm.Version?.Trim();
        asset.Edition = vm.Edition?.Trim();
        asset.BuildNumber = vm.BuildNumber?.Trim();
        asset.LicenseType = vm.LicenseType?.Trim();
        asset.LicenseKey = vm.LicenseKey?.Trim();
        asset.InstalledOn = vm.InstalledOn?.Trim();
        asset.SupportEndDate = vm.SupportEndDate;
        asset.PatchStatus = vm.PatchStatus?.Trim();
        asset.PurchaseDate = vm.PurchaseDate;
        asset.SoftwareStatus = vm.Status;
        asset.SoftwareNotes = vm.Notes?.Trim();
        asset.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplySubscriptionToAsset(Asset asset, CreateSubscriptionVm vm)
    {
        asset.AssetTag = vm.AssetTag.Trim();
        asset.AssetType = AssetType.Software;
        asset.Brand = "Software";
        asset.Model = vm.Name.Trim();
        asset.Status = MapAssetStatus(vm.Status);
        asset.Location = string.IsNullOrWhiteSpace(vm.Location) ? "Not specified" : vm.Location.Trim();
        asset.Condition = "N/A";
        asset.TopLevelCategory = SoftwareTopLevelKey;
        asset.Category = ResolveSoftwareCategoryKey(SoftwareCategory.Subscriptions);
        asset.SubCategory = null;
        asset.VendorId = vm.VendorId;
        asset.SoftwareCategory = SoftwareCategory.Subscriptions;
        asset.SoftwareName = vm.Name.Trim();
        asset.BillingCycle = vm.BillingCycle;
        asset.StartDate = vm.StartDate;
        asset.ExpiryDate = vm.ExpiryDate;
        asset.AutoRenew = vm.AutoRenew;
        asset.RenewalReminderDate = vm.RenewalReminderDate;
        asset.Cost = vm.Cost;
        asset.Currency = string.IsNullOrWhiteSpace(vm.Currency) ? "KES" : vm.Currency.Trim();
        asset.InvoiceReference = vm.InvoiceReference?.Trim();
        asset.PurchaseOrderReference = vm.PurchaseOrderReference?.Trim();
        asset.AssignedToUserOrDepartment = vm.AssignedToUserOrDepartment?.Trim();
        asset.InstalledOn = vm.InstalledOn?.Trim();
        asset.SoftwareStatus = vm.Status;
        asset.SoftwareNotes = vm.Notes?.Trim();
        asset.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplySubscriptionToAsset(Asset asset, EditSubscriptionVm vm)
    {
        ApplySubscriptionToAsset(asset, (CreateSubscriptionVm)vm);
        asset.PlanOrTier = vm.PlanOrTier?.Trim();
    }

    private static void ApplyOpenSourceToAsset(Asset asset, CreateOpenSourceVm vm)
    {
        asset.AssetTag = vm.AssetTag.Trim();
        asset.AssetType = AssetType.Software;
        asset.Brand = "Software";
        asset.Model = vm.Name.Trim();
        asset.Status = MapAssetStatus(vm.Status);
        asset.Location = string.IsNullOrWhiteSpace(vm.Location) ? "Not specified" : vm.Location.Trim();
        asset.Condition = "N/A";
        asset.TopLevelCategory = SoftwareTopLevelKey;
        asset.Category = ResolveSoftwareCategoryKey(SoftwareCategory.OpenSource);
        asset.SubCategory = null;
        asset.VendorId = vm.VendorId;
        asset.SoftwareCategory = SoftwareCategory.OpenSource;
        asset.SoftwareName = vm.Name.Trim();
        asset.SoftwareVersion = vm.Version?.Trim();
        asset.OpenSourceLicenseType = vm.OpenSourceLicenseType?.Trim();
        asset.MaintainerOrVendor = vm.MaintainerOrVendor?.Trim();
        asset.InstalledOn = vm.InstalledOn?.Trim();
        asset.DeploymentEnvironment = vm.DeploymentEnvironment;
        asset.SecurityStatus = vm.SecurityStatus?.Trim();
        asset.LastUpdatedDate = vm.LastUpdatedDate;
        asset.SoftwareStatus = vm.Status;
        asset.SoftwareNotes = vm.Notes?.Trim();
        asset.UpdatedAt = DateTime.UtcNow;
    }
}
