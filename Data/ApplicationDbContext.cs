using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<tblUser> tblUsers { get; set; }
        public DbSet<tblTenant> tblTenants { get; set; }
        public DbSet<tblBilling> tblBillings { get; set; }
        public DbSet<tblLostFoundItem> tblLostFoundItems { get; set; }

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

            modelBuilder.Entity<tblTenant>(entity =>
            {
                entity.HasKey(e => e.TenantID);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ContactNumber).HasMaxLength(20);
                entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
                entity.Property(e => e.EmergencyContactRelationship).HasMaxLength(50);
                entity.Property(e => e.EmergencyContactNumber).HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
            });

            modelBuilder.Entity<tblBilling>(entity =>
            {
                entity.HasKey(e => e.BillingID);
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(b => b.Tenant)
                      .WithMany()
                      .HasForeignKey(b => b.TenantID)
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
        }
    }
}
