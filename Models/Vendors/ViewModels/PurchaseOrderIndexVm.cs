namespace AssetTracker.Models.Vendors.ViewModels;

public sealed class PurchaseOrderIndexVm
{
    public string? Query { get; init; }
    public PurchaseOrderStatus? StatusFilter { get; init; }
    public int TotalOrders { get; init; }
    public int OpenOrders { get; init; }
    public int ReceivedOrders { get; init; }
    public decimal YtdSpend { get; init; }
    public IReadOnlyList<PurchaseOrderListItemVm> Items { get; init; } = [];
}
