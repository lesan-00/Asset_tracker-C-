using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using AssetTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VendorSupportTicketsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IVendorCodeGenerator _codeGenerator;

    public VendorSupportTicketsController(ApplicationDbContext context, IVendorCodeGenerator codeGenerator)
    {
        _context = context;
        _codeGenerator = codeGenerator;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int vendorId)
    {
        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vendorId && !v.IsArchived);
        if (vendor is null)
        {
            return NotFound();
        }

        await PopulateAssetOptionsAsync(vendorId, null);
        ViewData["VendorLabel"] = $"{vendor.CompanyName} ({vendor.VendorCode})";
        return View(new VendorSupportTicketCreateVm
        {
            VendorId = vendorId,
            OpenedDate = DateTime.UtcNow.Date,
            Status = SupportTicketStatus.Open
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(VendorSupportTicketCreateVm vm)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == vm.VendorId && !v.IsArchived);
        if (vendor is null)
        {
            ModelState.AddModelError(nameof(VendorSupportTicketCreateVm.VendorId), "Selected vendor does not exist.");
        }

        if (vm.ClosedDate.HasValue && vm.ClosedDate.Value.Date < vm.OpenedDate.Date)
        {
            ModelState.AddModelError(nameof(VendorSupportTicketCreateVm.ClosedDate), "Closed date cannot be before opened date.");
        }

        if (vm.AssetId.HasValue && !await _context.Assets.AnyAsync(a => a.Id == vm.AssetId.Value))
        {
            ModelState.AddModelError(nameof(VendorSupportTicketCreateVm.AssetId), "Selected asset does not exist.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssetOptionsAsync(vm.VendorId, vm.AssetId);
            ViewData["VendorLabel"] = vendor is null ? "Vendor" : $"{vendor.CompanyName} ({vendor.VendorCode})";
            return View(vm);
        }

        var ticket = new VendorSupportTicket
        {
            VendorId = vm.VendorId,
            TicketNumber = await _codeGenerator.NextSupportTicketNumberAsync(),
            Type = vm.Type,
            Title = vm.Title.Trim(),
            OpenedDate = vm.OpenedDate.Date,
            ClosedDate = vm.ClosedDate?.Date,
            Status = vm.Status,
            AssetId = vm.AssetId,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.VendorSupportTickets.Add(ticket);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Vendors", new { id = vm.VendorId });
    }

    private async Task PopulateAssetOptionsAsync(int vendorId, int? selectedAssetId)
    {
        var assets = await _context.Assets
            .AsNoTracking()
            .Where(a => a.VendorId == vendorId)
            .OrderBy(a => a.AssetTag)
            .Select(a => new
            {
                a.Id,
                Label = $"{a.AssetTag} ({a.SerialNumber})"
            })
            .ToListAsync();

        ViewData["AssetId"] = new SelectList(assets, "Id", "Label", selectedAssetId);
    }
}
