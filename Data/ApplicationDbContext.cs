using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<tblUser> tblUsers { get; set; }
        public DbSet<tblUnit> tblUnits { get; set; }
        public DbSet<tblTenant> tblTenants { get; set; }
        public DbSet<tblBilling> tblBillings { get; set; }
        public DbSet<tblMaintenanceRequest> tblMaintenanceRequests { get; set; }
        public DbSet<tblLostFoundItem> tblLostFoundItems { get; set; }
        public DbSet<tblClaimRequest> tblClaimRequests { get; set; }
        public DbSet<tblUnitTransferRequest> tblUnitTransferRequests { get; set; }
        public DbSet<tblNotification> tblNotifications { get; set; }

        // SQLite has no native decimal type. Store money values as REAL (double) so that
        // comparisons and ordering in queries work numerically; stored as TEXT (the EF
        // default for SQLite) they would sort lexicographically and break billing logic.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<tblUser>(entity =>
            {
                entity.HasKey(e => e.UserID);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<tblUnit>(entity =>
            {
                entity.HasKey(e => e.UnitID);
                entity.Property(e => e.UnitNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Available");
                entity.HasIndex(e => e.UnitNumber).IsUnique();
            });

            modelBuilder.Entity<tblTenant>(entity =>
            {
                entity.HasKey(e => e.TenantID);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ContactNumber).HasMaxLength(20);
                entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
                entity.Property(e => e.EmergencyContactRelationship).HasMaxLength(50);
                entity.Property(e => e.EmergencyContactNumber).HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");

                entity.HasOne(t => t.User)
                      .WithMany(u => u.Tenants)
                      .HasForeignKey(t => t.UserID)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.Unit)
                      .WithMany(u => u.Tenants)
                      .HasForeignKey(t => t.UnitID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblBilling>(entity =>
            {
                entity.HasKey(e => e.BillingID);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Unpaid");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(b => b.Tenant)
                      .WithMany()
                      .HasForeignKey(b => b.TenantID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<tblMaintenanceRequest>(entity =>
            {
                entity.HasKey(e => e.RequestID);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Priority).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.HasOne(m => m.Tenant)
                      .WithMany()
                      .HasForeignKey(m => m.TenantID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<tblLostFoundItem>(entity =>
            {
                entity.HasKey(e => e.ItemID);
                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ItemType).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Location).HasMaxLength(200);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Reported");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(l => l.ReportedBy)
                      .WithMany()
                      .HasForeignKey(l => l.ReportedByUserID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblClaimRequest>(entity =>
            {
                entity.HasKey(e => e.ClaimID);
                entity.Property(e => e.VerificationDetails).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.HasOne(c => c.Item)
                      .WithMany()
                      .HasForeignKey(c => c.ItemID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Claimant)
                      .WithMany()
                      .HasForeignKey(c => c.ClaimantUserID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblUnitTransferRequest>(entity =>
            {
                entity.HasKey(e => e.TransferID);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.HasOne(r => r.Tenant)
                      .WithMany()
                      .HasForeignKey(r => r.TenantID)
                      .OnDelete(DeleteBehavior.Cascade);

                // two FKs into tblUnits — keep them non-cascading to avoid multiple cascade paths
                entity.HasOne(r => r.CurrentUnit)
                      .WithMany()
                      .HasForeignKey(r => r.CurrentUnitID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.RequestedUnit)
                      .WithMany()
                      .HasForeignKey(r => r.RequestedUnitID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblNotification>(entity =>
            {
                entity.HasKey(e => e.NotificationID);
                entity.Property(e => e.Module).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Message).IsRequired().HasMaxLength(300);
                entity.Property(e => e.Link).HasMaxLength(300);
                entity.HasIndex(e => new { e.UserID, e.IsRead });

                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserID)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
