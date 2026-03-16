using AssetTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Services;

public sealed class VendorCodeGenerator : IVendorCodeGenerator
{
    private readonly ApplicationDbContext _context;

    public VendorCodeGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextVendorCodeAsync()
    {
        var nextNumber = (await _context.Vendors.AsNoTracking().MaxAsync(v => (int?)v.Id) ?? 0) + 1;
        return $"VND-{nextNumber:00000}";
    }

    public async Task<string> NextPurchaseRequestNumberAsync()
    {
        var nextNumber = (await _context.PurchaseRequests.AsNoTracking().MaxAsync(pr => (int?)pr.Id) ?? 0) + 1;
        return $"PR-{nextNumber:00000}";
    }

    public async Task<string> NextPurchaseOrderNumberAsync()
    {
        var nextNumber = (await _context.PurchaseOrders.AsNoTracking().MaxAsync(po => (int?)po.Id) ?? 0) + 1;
        return $"PO-{nextNumber:00000}";
    }

    public async Task<string> NextSupportTicketNumberAsync()
    {
        var nextNumber = (await _context.VendorSupportTickets.AsNoTracking().MaxAsync(t => (int?)t.Id) ?? 0) + 1;
        return $"TKT-{nextNumber:00000}";
    }
}
