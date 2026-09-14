using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Data
{
    // Maps onto the database built by Database/backupDB.sql. The application
    // never creates the schema — the script does — so everything here describes
    // tables that already exist, and the names must match it exactly.
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<TenantProfile> TenantProfiles { get; set; }
        public DbSet<TenantUnitAssignment> TenantUnitAssignments { get; set; }
        public DbSet<LostFoundItem> LostFoundItems { get; set; }
        public DbSet<ClaimRequest> ClaimRequests { get; set; }
        public DbSet<UnitTransferRequest> UnitTransferRequests { get; set; }
        public DbSet<Billing> Billings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Roles");
                e.HasKey(x => x.RoleID);
                e.HasIndex(x => x.RoleName).IsUnique();
            });

            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.UserID);
                e.HasIndex(x => x.Username).IsUnique();

                e.HasOne(x => x.Role)
                 .WithMany(r => r.Users)
                 .HasForeignKey(x => x.RoleID)
                 .OnDelete(DeleteBehavior.Restrict);

                // the database stamps these itself
                e.Property(x => x.DateCreated).ValueGeneratedOnAdd();
                e.Property(x => x.DateUpdated).ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<Unit>(e =>
            {
                e.ToTable("Units");
                e.HasKey(x => x.UnitID);
                e.HasIndex(x => x.UnitNumber).IsUnique();
                e.Property(x => x.DateAdded).ValueGeneratedOnAdd();
                e.Property(x => x.DateUpdated).ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<TenantProfile>(e =>
            {
                e.ToTable("TenantProfiles");
                e.HasKey(x => x.TenantID);
                e.HasIndex(x => x.UserID).IsUnique();

                e.HasOne(x => x.User)
                 .WithOne(u => u.TenantProfile)
                 .HasForeignKey<TenantProfile>(x => x.UserID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.DateRecorded).ValueGeneratedOnAdd();
                e.Property(x => x.DateUpdated).ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<TenantUnitAssignment>(e =>
            {
                e.ToTable("TenantUnitAssignments");
                e.HasKey(x => x.AssignmentID);

                e.HasOne(x => x.Tenant)
                 .WithMany(t => t.Assignments)
                 .HasForeignKey(x => x.TenantID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Unit)
                 .WithMany(u => u.Assignments)
                 .HasForeignKey(x => x.UnitID)
                 .OnDelete(DeleteBehavior.Restrict);

                // ActiveTenantID is a STORED GENERATED column: the database
                // computes it and a UNIQUE key on it is what stops a tenant
                // holding two Active assignments. EF must never write it, and
                // does not need to read it, so it is not mapped at all.
                e.Ignore("ActiveTenantID");

                e.Property(x => x.DateRecorded).ValueGeneratedOnAdd();
                e.Property(x => x.DateUpdated).ValueGeneratedOnAddOrUpdate();
            });

            modelBuilder.Entity<LostFoundItem>(e =>
            {
                e.ToTable("LostFoundItems");
                e.HasKey(x => x.ItemID);

                e.HasOne(x => x.ReportedBy)
                 .WithMany()
                 .HasForeignKey(x => x.ReportedByUserID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ClaimedBy)
                 .WithMany()
                 .HasForeignKey(x => x.ClaimedByUserID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.DateReported).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<ClaimRequest>(e =>
            {
                e.ToTable("ClaimRequests");
                e.HasKey(x => x.ClaimID);

                e.HasOne(x => x.Item)
                 .WithMany(i => i.Claims)
                 .HasForeignKey(x => x.ItemID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Claimant)
                 .WithMany()
                 .HasForeignKey(x => x.ClaimantUserID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.SubmittedAt).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<UnitTransferRequest>(e =>
            {
                e.ToTable("UnitTransferRequests");
                e.HasKey(x => x.TransferID);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.RequestedUnit)
                 .WithMany()
                 .HasForeignKey(x => x.RequestedUnitID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CurrentUnit)
                 .WithMany()
                 .HasForeignKey(x => x.CurrentUnitID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.DateRequested).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<Billing>(e =>
            {
                e.ToTable("Billings");
                e.HasKey(x => x.BillingID);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.BillingPeriod).HasColumnType("date");
                e.Property(x => x.DueDate).HasColumnType("date");
                e.Property(x => x.DateIssued).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<Payment>(e =>
            {
                e.ToTable("Payments");
                e.HasKey(x => x.PaymentID);

                e.HasOne(x => x.Billing)
                 .WithMany(b => b.Payments)
                 .HasForeignKey(x => x.BillingID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.DatePaid).ValueGeneratedOnAdd();
                e.Property(x => x.RecordedAt).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<MaintenanceRequest>(e =>
            {
                e.ToTable("MaintenanceRequests");
                e.HasKey(x => x.RequestID);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Unit)
                 .WithMany()
                 .HasForeignKey(x => x.UnitID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.AssignedStaff)
                 .WithMany()
                 .HasForeignKey(x => x.AssignedStaffUserID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.DateSubmitted).ValueGeneratedOnAdd();
            });
        }
    }
}
