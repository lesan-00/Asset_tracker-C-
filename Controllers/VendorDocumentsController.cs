using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VendorDocumentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public VendorDocumentsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
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

        ViewData["VendorLabel"] = $"{vendor.CompanyName} ({vendor.VendorCode})";
        return View(new VendorDocumentCreateVm
        {
            VendorId = vendorId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(VendorDocumentCreateVm vm, IFormFile? upload)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == vm.VendorId && !v.IsArchived);
        if (vendor is null)
        {
            ModelState.AddModelError(nameof(VendorDocumentCreateVm.VendorId), "Selected vendor does not exist.");
        }

        if (upload is not null && upload.Length > 0)
        {
            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "vendors");
            Directory.CreateDirectory(uploadsRoot);

            var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(upload.FileName)}";
            var fullPath = Path.Combine(uploadsRoot, safeName);
            await using var stream = System.IO.File.Create(fullPath);
            await upload.CopyToAsync(stream);
            vm.FilePath = $"/uploads/vendors/{safeName}";
        }

        if (!ModelState.IsValid)
        {
            ViewData["VendorLabel"] = vendor is null ? "Vendor" : $"{vendor.CompanyName} ({vendor.VendorCode})";
            return View(vm);
        }

        var document = new VendorDocument
        {
            VendorId = vm.VendorId,
            DocumentName = vm.DocumentName.Trim(),
            DocumentType = vm.DocumentType.Trim(),
            FilePath = string.IsNullOrWhiteSpace(vm.FilePath) ? null : vm.FilePath.Trim(),
            UploadedAt = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim()
        };

        _context.VendorDocuments.Add(document);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Vendors", new { id = vm.VendorId });
    }
}
