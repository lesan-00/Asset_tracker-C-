using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AssetTracker.Models;

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
        }
    }
}
