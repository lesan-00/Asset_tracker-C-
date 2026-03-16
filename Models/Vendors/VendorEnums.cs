using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Vendors;

public enum VendorStatus
{
    Active,
    Inactive,
    Blacklisted
}

public enum VendorCategory
{
    HardwareSupplier,
    SoftwareVendor,
    FurnitureSupplier,
    ServiceProvider,
    Other
}

public enum InvoicePaymentStatus
{
    Pending,
    Paid,
    Overdue,
    Cancelled
}

public enum ContractStatus
{
    Active,
    Expired,
    PendingRenewal,
    Terminated
}

public enum ContractType
{
    [Display(Name = "Supply Agreement")]
    SupplyAgreement,
    [Display(Name = "SLA")]
    Sla,
    [Display(Name = "Maintenance")]
    Maintenance,
    [Display(Name = "NDA")]
    Nda,
    [Display(Name = "Service Contract")]
    ServiceContract,
    [Display(Name = "Lease")]
    Lease,
    [Display(Name = "Other")]
    Other
}

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    Invoiced,
    Paid,
    Closed,
    Cancelled,
    [Display(Name = "Approved (Legacy)")]
    Approved,
    [Display(Name = "Received (Legacy)")]
    Received
}

public enum SupportTicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum SupportTicketType
{
    [Display(Name = "Warranty Claim")]
    WarrantyClaim,
    Rma,
    Support
}

public enum PurchaseRequestStatus
{
    Draft,
    Submitted,
    PendingApproval,
    Approved,
    Rejected,
    ConvertedToPO,
    Cancelled,
    Closed
}

public enum PurchaseRequestPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum PurchaseRequestLineType
{
    Hardware,
    Software,
    Service,
    Furniture,
    [Display(Name = "Office Supplies")]
    OfficeSupplies,
    Accessories,
    Other,
    Item,
}
