namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseRequestIndexVm
{
    public string? Query { get; init; }
    public PurchaseRequestStatus? StatusFilter { get; init; }
    public int TotalRequests { get; init; }
    public int PendingApproval { get; init; }
    public int Approved { get; init; }
    public int ConvertedToPo { get; init; }
    public IReadOnlyList<PurchaseRequestListItemVm> Items { get; init; } = [];
}
