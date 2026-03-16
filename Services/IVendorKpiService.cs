using AssetTracker.Models.Vendors.ViewModels;

namespace AssetTracker.Services;

public interface IVendorKpiService
{
    Task<VendorKpiVm> BuildSummaryAsync();
}
