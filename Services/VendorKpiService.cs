using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using AssetTracker.Models.Vendors.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Services;

public sealed class VendorKpiService : IVendorKpiService
{
    private readonly ApplicationDbContext _context;

    public VendorKpiService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorKpiVm> BuildSummaryAsync()
    {
        var totalVendors = await _context.Vendors
            .AsNoTracking()
            .CountAsync(v => !v.IsArchived);

        var activeContracts = await _context.VendorContracts
            .AsNoTracking()
            .CountAsync(c => c.Status == ContractStatus.Active);

        var pendingInvoices = await _context.VendorInvoices
            .AsNoTracking()
            .CountAsync(i => i.PaymentStatus == InvoicePaymentStatus.Pending || i.PaymentStatus == InvoicePaymentStatus.Overdue);

        // Spend definition: sum of paid/closed purchase orders (plus legacy received).
        var totalSpend = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po =>
                po.Status == PurchaseOrderStatus.Paid ||
                po.Status == PurchaseOrderStatus.Closed ||
                po.Status == PurchaseOrderStatus.Received)
            .SumAsync(po => (decimal?)po.TotalAmount) ?? 0m;

        return new VendorKpiVm
        {
            TotalVendors = totalVendors,
            ActiveContracts = activeContracts,
            PendingInvoices = pendingInvoices,
            TotalSpend = totalSpend
        };
    }
}
