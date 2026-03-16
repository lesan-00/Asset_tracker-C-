namespace AssetTracker.Services;

public interface IVendorCodeGenerator
{
    Task<string> NextVendorCodeAsync();
    Task<string> NextPurchaseRequestNumberAsync();
    Task<string> NextPurchaseOrderNumberAsync();
    Task<string> NextSupportTicketNumberAsync();
}
