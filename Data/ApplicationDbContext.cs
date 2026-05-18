using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Models;

namespace YnclinoAMS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<tblUser> tblUsers { get; set; }
        public DbSet<tblUnit> tblUnits { get; set; }
        public DbSet<tblTenant> tblTenants { get; set; }

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
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Vacant");
                entity.HasIndex(e => e.UnitNumber).IsUnique();
            });

            modelBuilder.Entity<tblTenant>(entity =>
            {
                entity.HasKey(e => e.TenantID);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ContactNumber).HasMaxLength(20);
                entity.Property(e => e.EmergencyContact).HasMaxLength(100);
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
        }
    }
}
