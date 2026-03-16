namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorKpiVm
{
    public int TotalVendors { get; init; }
    public int ActiveContracts { get; init; }
    public int PendingInvoices { get; init; }
    public decimal TotalSpend { get; init; }
}
