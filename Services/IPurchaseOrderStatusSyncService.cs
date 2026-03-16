namespace AssetTracker.Services;

public interface IPurchaseOrderStatusSyncService
{
    Task SyncPurchaseOrderStatusAsync(int purchaseOrderId);
    Task SyncAllPurchaseOrderStatusesAsync();
}
