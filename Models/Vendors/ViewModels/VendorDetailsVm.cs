using AssetTracker.Models;

namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorDetailsVm
{
    public Vendor Vendor { get; init; } = default!;
    public decimal TotalSpend { get; init; }
    public DateTime? LastTransactionDate { get; init; }
    public int ActiveContracts { get; init; }
    public int PendingInvoices { get; init; }
    public int OverdueInvoices { get; init; }
    public IReadOnlyList<Asset> LinkedAssets { get; init; } = [];
    public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; init; } = [];
    public IReadOnlyList<VendorInvoice> Invoices { get; init; } = [];
    public IReadOnlyList<VendorContract> Contracts { get; init; } = [];
    public IReadOnlyList<VendorSupportTicket> SupportTickets { get; init; } = [];
    public IReadOnlyList<VendorDocument> Documents { get; init; } = [];
}
