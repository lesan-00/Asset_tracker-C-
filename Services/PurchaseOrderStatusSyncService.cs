using AssetTracker.Data;
using AssetTracker.Models.Vendors;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Services;

public sealed class PurchaseOrderStatusSyncService : IPurchaseOrderStatusSyncService
{
    private readonly ApplicationDbContext _context;

    public PurchaseOrderStatusSyncService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SyncPurchaseOrderStatusAsync(int purchaseOrderId)
    {
        if (purchaseOrderId <= 0)
        {
            return;
        }

        var purchaseOrder = await _context.PurchaseOrders
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);
        if (purchaseOrder is null)
        {
            return;
        }

        // Manual terminal states should not be auto-overwritten.
        if (purchaseOrder.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed)
        {
            return;
        }

        var invoices = await _context.VendorInvoices
            .AsNoTracking()
            .Where(i => i.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();

        var hasInvoices = invoices.Count > 0;
        var allPaid = hasInvoices && invoices.All(IsFullyPaidInvoice);

        PurchaseOrderStatus newStatus;
        if (allPaid)
        {
            newStatus = PurchaseOrderStatus.Paid;
        }
        else if (hasInvoices)
        {
            newStatus = PurchaseOrderStatus.Invoiced;
        }
        else if (purchaseOrder.Status == PurchaseOrderStatus.Received)
        {
            newStatus = PurchaseOrderStatus.Closed;
        }
        else if (IsSubmittedLike(purchaseOrder.Status))
        {
            newStatus = PurchaseOrderStatus.Submitted;
        }
        else
        {
            newStatus = PurchaseOrderStatus.Draft;
        }

        if (purchaseOrder.Status == newStatus)
        {
            return;
        }

        purchaseOrder.Status = newStatus;
        await _context.SaveChangesAsync();
    }

    public async Task SyncAllPurchaseOrderStatusesAsync()
    {
        var purchaseOrderIds = await _context.PurchaseOrders
            .AsNoTracking()
            .Select(po => po.Id)
            .ToListAsync();

        foreach (var id in purchaseOrderIds)
        {
            await SyncPurchaseOrderStatusAsync(id);
        }
    }

    private static bool IsFullyPaidInvoice(VendorInvoice invoice)
    {
        return invoice.PaymentStatus == InvoicePaymentStatus.Paid;
    }

    private static bool IsSubmittedLike(PurchaseOrderStatus status)
    {
        return status is PurchaseOrderStatus.Submitted
            or PurchaseOrderStatus.Invoiced
            or PurchaseOrderStatus.Paid
            or PurchaseOrderStatus.Approved;
    }
}
