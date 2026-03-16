using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AssetTracker.Models;
using AssetTracker.Models.Vendors;

namespace AssetTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Asset> Assets { get; set; } = default!;
        public DbSet<StaffProfile> StaffProfiles { get; set; } = default!;
        public DbSet<Assignment> Assignments { get; set; } = default!;
        public DbSet<Issue> Issues { get; set; } = default!;
        public DbSet<Vendor> Vendors { get; set; } = default!;
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; } = default!;
        public DbSet<PurchaseRequestLine> PurchaseRequestLines { get; set; } = default!;
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = default!;
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; } = default!;
        public DbSet<VendorInvoice> VendorInvoices { get; set; } = default!;
        public DbSet<VendorContract> VendorContracts { get; set; } = default!;
        public DbSet<VendorSupportTicket> VendorSupportTickets { get; set; } = default!;
        public DbSet<VendorDocument> VendorDocuments { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Asset>()
                .HasIndex(a => a.AssetTag)
                .IsUnique();

            builder.Entity<Asset>()
                .Property(a => a.AssetType)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<Asset>()
                .Property(a => a.Brand)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<Asset>()
                .Property(a => a.Model)
                .IsRequired()
                .HasMaxLength(100);

            builder.Entity<Asset>()
                .Property(a => a.Specifications)
                .HasMaxLength(1000);

            builder.Entity<Asset>()
                .Property(a => a.Cost)
                .HasPrecision(18, 2);

            builder.Entity<Asset>()
                .Property(a => a.SoftwareCategory)
                .HasConversion<string>()
                .HasMaxLength(40);

            builder.Entity<Asset>()
                .Property(a => a.SoftwareStatus)
                .HasConversion<string>()
                .HasMaxLength(40);

            builder.Entity<Asset>()
                .Property(a => a.BillingCycle)
                .HasConversion<string>()
                .HasMaxLength(30);

            builder.Entity<Asset>()
                .Property(a => a.DeploymentEnvironment)
                .HasConversion<string>()
                .HasMaxLength(30);

            builder.Entity<Asset>()
                .HasOne(a => a.Vendor)
                .WithMany(v => v.Assets)
                .HasForeignKey(a => a.VendorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<StaffProfile>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Assignment>()
                .HasOne(a => a.Asset)
                .WithMany()
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Assignment>()
                .HasOne(a => a.StaffProfile)
                .WithMany()
                .HasForeignKey(a => a.StaffProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Issue>()
                .HasOne(i => i.CreatedByUser)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Issue>()
                .HasOne(i => i.AssignedToUser)
                .WithMany()
                .HasForeignKey(i => i.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Issue>()
                .HasOne(i => i.Asset)
                .WithMany()
                .HasForeignKey(i => i.AssetId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Issue>()
                .HasOne(i => i.ReportedForStaffProfile)
                .WithMany()
                .HasForeignKey(i => i.ReportedForStaffProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Vendor>()
                .HasIndex(v => v.VendorCode)
                .IsUnique();

            builder.Entity<Vendor>()
                .Property(v => v.Category)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<Vendor>()
                .Property(v => v.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<PurchaseRequest>()
                .HasIndex(pr => pr.PrNumber)
                .IsUnique();

            builder.Entity<PurchaseRequest>()
                .Property(pr => pr.Priority)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<PurchaseRequest>()
                .Property(pr => pr.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<PurchaseRequest>()
                .HasOne(pr => pr.RequestedByStaff)
                .WithMany()
                .HasForeignKey(pr => pr.RequestedByStaffId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PurchaseRequestLine>()
                .Property(line => line.LineType)
                .HasConversion<string>()
                .HasMaxLength(30);

            builder.Entity<PurchaseRequestLine>()
                .Property(line => line.EstimatedUnitCost)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseRequestLine>()
                .Property(line => line.EstimatedTotal)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseRequestLine>()
                .HasOne(line => line.PurchaseRequest)
                .WithMany(pr => pr.Lines)
                .HasForeignKey(line => line.PurchaseRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PurchaseRequestLine>()
                .HasOne(line => line.SuggestedVendor)
                .WithMany()
                .HasForeignKey(line => line.SuggestedVendorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PurchaseOrder>()
                .HasIndex(po => po.PoNumber)
                .IsUnique();

            builder.Entity<PurchaseOrder>()
                .Property(po => po.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseOrder>()
                .Property(po => po.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<PurchaseOrder>()
                .HasOne(po => po.Vendor)
                .WithMany(v => v.PurchaseOrders)
                .HasForeignKey(po => po.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseOrder>()
                .HasOne(po => po.PurchaseRequest)
                .WithMany(pr => pr.PurchaseOrders)
                .HasForeignKey(po => po.PurchaseRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PurchaseOrderLine>()
                .Property(line => line.UnitPrice)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseOrderLine>()
                .Property(line => line.LineTotal)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseOrderLine>()
                .HasOne(line => line.PurchaseOrder)
                .WithMany(po => po.Lines)
                .HasForeignKey(line => line.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<VendorInvoice>()
                .HasIndex(i => new { i.VendorId, i.InvoiceNumber })
                .IsUnique();

            builder.Entity<VendorInvoice>()
                .Property(i => i.Amount)
                .HasPrecision(18, 2);

            builder.Entity<VendorInvoice>()
                .Property(i => i.PaymentStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<VendorInvoice>()
                .HasOne(i => i.Vendor)
                .WithMany(v => v.Invoices)
                .HasForeignKey(i => i.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VendorInvoice>()
                .HasOne(i => i.PurchaseOrder)
                .WithMany(po => po.Invoices)
                .HasForeignKey(i => i.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<VendorContract>()
                .Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<VendorContract>()
                .Property(c => c.ContractType)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<VendorContract>()
                .Property(c => c.ContractValue)
                .HasPrecision(18, 2);

            builder.Entity<VendorContract>()
                .HasIndex(c => c.ContractNumber)
                .IsUnique();

            builder.Entity<VendorContract>()
                .HasOne(c => c.Vendor)
                .WithMany(v => v.Contracts)
                .HasForeignKey(c => c.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VendorSupportTicket>()
                .HasIndex(t => t.TicketNumber)
                .IsUnique();

            builder.Entity<VendorSupportTicket>()
                .Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<VendorSupportTicket>()
                .Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Entity<VendorSupportTicket>()
                .HasOne(t => t.Vendor)
                .WithMany(v => v.SupportTickets)
                .HasForeignKey(t => t.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VendorSupportTicket>()
                .HasOne(t => t.Asset)
                .WithMany()
                .HasForeignKey(t => t.AssetId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<VendorDocument>()
                .HasOne(d => d.Vendor)
                .WithMany(v => v.Documents)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
