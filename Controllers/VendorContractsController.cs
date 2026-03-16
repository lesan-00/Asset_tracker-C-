using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AssetTracker.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VendorContractsController : Controller
{
    private readonly ApplicationDbContext _context;

    public VendorContractsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int vendorId)
    {
        var vendor = await GetActiveVendorAsync(vendorId);
        if (vendor is null)
        {
            return NotFound();
        }

        var vm = new VendorContractCreateVm
        {
            Id = null,
            VendorId = vendorId,
            ContractNumber = await GenerateNextContractNumberAsync(),
            ContractType = Models.Vendors.ContractType.SupplyAgreement,
            Currency = "KES",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1).AddDays(-1),
            AlertBeforeDays = 30,
            Status = ContractStatus.Active,
            VendorName = vendor.CompanyName,
            VendorCode = vendor.VendorCode
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(VendorContractCreateVm vm)
    {
        var vendor = await GetActiveVendorAsync(vm.VendorId);
        if (vendor is null)
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.VendorId), "Selected vendor does not exist.");
        }

        if (vm.ContractType is null)
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.ContractType), "Contract type is required.");
        }

        if (string.IsNullOrWhiteSpace(vm.Currency))
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.Currency), "Currency is required.");
        }

        if (vm.EndDate.Date < vm.StartDate.Date)
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.EndDate), "Contract end date cannot be before start date.");
        }

        PopulateVendorContext(vm, vendor);

        if (!ModelState.IsValid)
        {
            if (string.IsNullOrWhiteSpace(vm.ContractNumber))
            {
                vm.ContractNumber = await GenerateNextContractNumberAsync();
            }

            return View(vm);
        }

        var contractNumber = vm.ContractNumber?.Trim();
        if (string.IsNullOrWhiteSpace(contractNumber) ||
            await _context.VendorContracts.AnyAsync(c => c.ContractNumber == contractNumber))
        {
            contractNumber = await GenerateNextContractNumberAsync();
        }

        var contract = new VendorContract
        {
            VendorId = vm.VendorId,
            ContractNumber = contractNumber,
            ContractTitle = vm.ContractTitle.Trim(),
            ContractType = vm.ContractType,
            ContractValue = vm.ContractValue,
            Currency = vm.Currency.Trim().ToUpperInvariant(),
            SignedBy = string.IsNullOrWhiteSpace(vm.SignedBy) ? null : vm.SignedBy.Trim(),
            SignedDate = vm.SignedDate?.Date,
            StartDate = vm.StartDate.Date,
            EndDate = vm.EndDate.Date,
            Status = vm.Status,
            AutoRenew = vm.AutoRenew,
            AlertBeforeDays = vm.AlertBeforeDays,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.VendorContracts.Add(contract);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Vendors", new { id = vm.VendorId });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var contract = await _context.VendorContracts
            .AsNoTracking()
            .Include(c => c.Vendor)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract is null || contract.Vendor.IsArchived)
        {
            return NotFound();
        }

        return View(new VendorContractCreateVm
        {
            Id = contract.Id,
            VendorId = contract.VendorId,
            ContractNumber = contract.ContractNumber ?? await GenerateNextContractNumberAsync(),
            ContractTitle = contract.ContractTitle,
            ContractType = contract.ContractType,
            ContractValue = contract.ContractValue,
            Currency = string.IsNullOrWhiteSpace(contract.Currency) ? "KES" : contract.Currency,
            SignedBy = contract.SignedBy,
            SignedDate = contract.SignedDate,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            Status = contract.Status,
            AutoRenew = contract.AutoRenew,
            AlertBeforeDays = contract.AlertBeforeDays,
            Notes = contract.Notes,
            VendorName = contract.Vendor.CompanyName,
            VendorCode = contract.Vendor.VendorCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, VendorContractCreateVm vm)
    {
        var contract = await _context.VendorContracts
            .Include(c => c.Vendor)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract is null || contract.Vendor.IsArchived)
        {
            return NotFound();
        }

        if (vm.ContractType is null)
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.ContractType), "Contract type is required.");
        }

        if (string.IsNullOrWhiteSpace(vm.Currency))
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.Currency), "Currency is required.");
        }

        if (vm.EndDate.Date < vm.StartDate.Date)
        {
            ModelState.AddModelError(nameof(VendorContractCreateVm.EndDate), "Contract end date cannot be before start date.");
        }

        PopulateVendorContext(vm, contract.Vendor);

        if (!ModelState.IsValid)
        {
            vm.Id = id;
            if (string.IsNullOrWhiteSpace(vm.ContractNumber))
            {
                vm.ContractNumber = contract.ContractNumber ?? await GenerateNextContractNumberAsync();
            }

            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(contract.ContractNumber))
        {
            contract.ContractNumber = await GenerateNextContractNumberAsync();
        }

        contract.ContractTitle = vm.ContractTitle.Trim();
        contract.ContractType = vm.ContractType;
        contract.ContractValue = vm.ContractValue;
        contract.Currency = vm.Currency.Trim().ToUpperInvariant();
        contract.SignedBy = string.IsNullOrWhiteSpace(vm.SignedBy) ? null : vm.SignedBy.Trim();
        contract.SignedDate = vm.SignedDate?.Date;
        contract.StartDate = vm.StartDate.Date;
        contract.EndDate = vm.EndDate.Date;
        contract.Status = vm.Status;
        contract.AutoRenew = vm.AutoRenew;
        contract.AlertBeforeDays = vm.AlertBeforeDays;
        contract.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Vendors", new { id = contract.VendorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Renew(int id)
    {
        var contract = await _context.VendorContracts.FirstOrDefaultAsync(c => c.Id == id);
        if (contract is null)
        {
            return NotFound();
        }

        var nextStart = contract.EndDate.Date.AddDays(1);
        var renewal = new VendorContract
        {
            VendorId = contract.VendorId,
            ContractNumber = await GenerateNextContractNumberAsync(),
            ContractTitle = $"{contract.ContractTitle} (Renewal)",
            ContractType = contract.ContractType,
            ContractValue = contract.ContractValue,
            Currency = string.IsNullOrWhiteSpace(contract.Currency) ? "KES" : contract.Currency,
            SignedBy = null,
            SignedDate = null,
            StartDate = nextStart,
            EndDate = nextStart.AddYears(1).AddDays(-1),
            Status = ContractStatus.Active,
            AutoRenew = contract.AutoRenew,
            AlertBeforeDays = contract.AlertBeforeDays,
            Notes = "Auto-created from Renew Contract action.",
            CreatedAt = DateTime.UtcNow
        };

        if (contract.EndDate.Date >= DateTime.UtcNow.Date)
        {
            contract.Status = ContractStatus.PendingRenewal;
        }
        else
        {
            contract.Status = ContractStatus.Expired;
        }

        _context.VendorContracts.Add(renewal);
        await _context.SaveChangesAsync();
        return RedirectToAction("Details", "Vendors", new { id = contract.VendorId });
    }

    private async Task<Vendor?> GetActiveVendorAsync(int vendorId)
    {
        return await _context.Vendors
            .FirstOrDefaultAsync(v => v.Id == vendorId && !v.IsArchived);
    }

    private static void PopulateVendorContext(VendorContractCreateVm vm, Vendor? vendor)
    {
        vm.VendorName = vendor?.CompanyName ?? string.Empty;
        vm.VendorCode = vendor?.VendorCode ?? string.Empty;
    }

    private async Task<string> GenerateNextContractNumberAsync()
    {
        var contractNumbers = await _context.VendorContracts
            .AsNoTracking()
            .Where(c => c.ContractNumber != null && c.ContractNumber.StartsWith("CTR-"))
            .Select(c => c.ContractNumber!)
            .ToListAsync();

        var max = 0;
        foreach (var value in contractNumbers)
        {
            var suffix = value.Length > 4 ? value[4..] : string.Empty;
            if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > max)
            {
                max = parsed;
            }
        }

        return $"CTR-{(max + 1):D5}";
    }
}
