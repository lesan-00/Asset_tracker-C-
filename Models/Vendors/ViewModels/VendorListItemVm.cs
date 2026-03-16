namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class VendorListItemVm
{
    public int Id { get; init; }
    public string VendorCode { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public VendorCategory Category { get; init; }
    public VendorStatus Status { get; init; }
    public bool IsPreferredVendor { get; init; }
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public DateTime? LastTransactionDate { get; init; }
    public ContractStatus? ContractStatus { get; init; }
    public decimal SpendAmount { get; init; }
}
