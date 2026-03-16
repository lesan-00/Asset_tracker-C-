using AssetTracker.Data;
using AssetTracker.Models.Procurement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.ViewComponents;

public sealed class ProcurementSidebarNavViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public ProcurementSidebarNavViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentController = ViewContext.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var currentAction = ViewContext.RouteData.Values["action"]?.ToString() ?? string.Empty;

        var isPurchaseRequestsController = string.Equals(currentController, "PurchaseRequests", StringComparison.OrdinalIgnoreCase);
        var isPurchaseRequestsActive = string.Equals(currentController, "Procurement", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(currentAction, "Requests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentAction, "Pipeline", StringComparison.OrdinalIgnoreCase));
        var isPurchaseOrdersActive = string.Equals(currentController, "PurchaseOrders", StringComparison.OrdinalIgnoreCase);
        var isInvoicesActive = string.Equals(currentController, "VendorInvoices", StringComparison.OrdinalIgnoreCase);
        var isNewRequestActive = isPurchaseRequestsController
            && string.Equals(currentAction, "Create", StringComparison.OrdinalIgnoreCase);
        if (isPurchaseRequestsController && !isNewRequestActive)
        {
            isPurchaseRequestsActive = true;
        }

        var isAnyActive = isPurchaseRequestsActive || isPurchaseOrdersActive || isInvoicesActive || isNewRequestActive || isPurchaseRequestsController;

        var prCount = await _context.PurchaseRequests.AsNoTracking().CountAsync();
        var poCount = await _context.PurchaseOrders.AsNoTracking().CountAsync();
        var invoiceCount = await _context.VendorInvoices.AsNoTracking().CountAsync();

        var model = new ProcurementSidebarNavVm
        {
            TotalCount = prCount + poCount + invoiceCount,
            PurchaseRequestCount = prCount,
            PurchaseOrderCount = poCount,
            InvoiceCount = invoiceCount,
            IsExpanded = isAnyActive,
            IsAnyActive = isAnyActive,
            IsPurchaseRequestsActive = isPurchaseRequestsActive,
            IsPurchaseOrdersActive = isPurchaseOrdersActive,
            IsInvoicesActive = isInvoicesActive,
            IsNewRequestActive = isNewRequestActive
        };

        return View(model);
    }
}
