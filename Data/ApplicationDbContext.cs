using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        public DbSet<tblPayment> tblPayments { get; set; }
        public DbSet<TenantUnitAssignment> TenantUnitAssignments { get; set; }

        // These values are display/edit conveniences on tblTenant; the assignment
        // row is their only persisted source.
        public async Task LoadTenantDatesAsync(IEnumerable<tblTenant> tenants, CancellationToken cancellationToken = default)
        {
            var list = tenants.ToList();
            var ids = list.Select(t => t.TenantID).ToList();
            if (ids.Count == 0) return;
            var assignments = await TenantUnitAssignments.AsNoTracking()
                .Where(a => ids.Contains(a.TenantID))
                .OrderByDescending(a => a.Status == "Active")
                .ThenByDescending(a => a.AssignmentID)
                .ToListAsync(cancellationToken);
            var latest = assignments.GroupBy(a => a.TenantID)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var tenant in list)
            {
                if (!latest.TryGetValue(tenant.TenantID, out var assignment)) continue;
                tenant.MoveInDate = assignment.MoveInDate;
                tenant.MoveOutDate = assignment.MoveOutDate;
                tenant.LeaseStart = assignment.LeaseStart;
                tenant.LeaseEnd = assignment.LeaseEnd;
            }
        }

        public Task LoadTenantDatesAsync(tblTenant tenant, CancellationToken cancellationToken = default) =>
            LoadTenantDatesAsync(new[] { tenant }, cancellationToken);

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var changedTenants = ChangeTracker.Entries<tblTenant>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Select(e => new
                {
                    Entry = e,
                    OldUnit = e.State == EntityState.Added ? null : e.OriginalValues.GetValue<int?>(nameof(tblTenant.UnitID)),
                    OldStatus = e.State == EntityState.Added ? null : e.OriginalValues.GetValue<string>(nameof(tblTenant.Status)),
                    IsNew = e.State == EntityState.Added
                }).ToList();

            foreach (var change in changedTenants)
            {
                var tenant = change.Entry.Entity;
                var user = tenant.User ?? (tenant.UserID.HasValue
                    ? await tblUsers.FindAsync(new object[] { tenant.UserID.Value }, cancellationToken)
                    : null);
                if (user != null)
                {
                    user.FirstName = tenant.FirstName;
                    user.LastName = tenant.LastName;
                    user.ContactNumber = tenant.ContactNumber;
                }
            }

            var ownsTransaction = Database.CurrentTransaction == null && changedTenants.Count > 0;
            await using var transaction = ownsTransaction
                ? await Database.BeginTransactionAsync(cancellationToken)
                : null;

            var count = await base.SaveChangesAsync(cancellationToken);
            foreach (var change in changedTenants)
            {
                var tenant = change.Entry.Entity;
                bool wasAssigned = change.OldStatus == "Active" && change.OldUnit.HasValue;
                bool isAssigned = tenant.Status == "Active" && tenant.UnitID.HasValue;
                bool unitChanged = change.OldUnit != tenant.UnitID;
                bool statusChanged = wasAssigned != isAssigned;
                if (!change.IsNew && !unitChanged && !statusChanged) continue;

                await TenantUnitAssignments
                    .Where(a => a.TenantID == tenant.TenantID && a.Status == "Active")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Status, "Ended")
                        .SetProperty(a => a.MoveOutDate, tenant.MoveOutDate ?? DateTime.Now), cancellationToken);

                if (isAssigned)
                {
                    TenantUnitAssignments.Add(new TenantUnitAssignment
                    {
                        TenantID = tenant.TenantID,
                        UnitID = tenant.UnitID!.Value,
                        MoveInDate = tenant.MoveInDate ?? DateTime.Now,
                        LeaseStart = tenant.LeaseStart,
                        LeaseEnd = tenant.LeaseEnd
                    });
                }
            }

            if (ChangeTracker.Entries<TenantUnitAssignment>().Any(e => e.State == EntityState.Added))
                count += await base.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return count;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<tblUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserID);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).HasColumnName("PasswordHash").IsRequired().HasMaxLength(255);
                entity.Property(e => e.Role).HasColumnName("RoleID").HasConversion<byte>(
                    role => role == "Admin" ? (byte)1 : role == "Maintenance" ? (byte)2 : (byte)3,
                    id => id == 1 ? "Admin" : id == 2 ? "Maintenance" : "Tenant");
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(80);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(80);
                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<tblUnit>(entity =>
            {
                entity.ToTable("Units");
                entity.HasKey(e => e.UnitID);
                entity.Property(e => e.UnitNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UnitType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Available");
                entity.HasIndex(e => e.UnitNumber).IsUnique();
            });

            modelBuilder.Entity<tblTenant>(entity =>
            {
                entity.ToTable("TenantProfiles");
                entity.HasKey(e => e.TenantID);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(80);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(80);
                entity.Property(e => e.ContactNumber).HasMaxLength(30);
                entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
                entity.Property(e => e.EmergencyContactRelationship).HasMaxLength(50);
                entity.Property(e => e.EmergencyContactNumber).HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");

                entity.HasOne(t => t.User)
                      .WithMany(u => u.Tenants)
                      .HasForeignKey(t => t.UserID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Unit)
                      .WithMany(u => u.Tenants)
                      .HasForeignKey(t => t.UnitID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblBilling>(entity =>
            {
                entity.ToTable("Billings");
                entity.HasKey(e => e.BillingID);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Unpaid");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(b => b.Tenant)
                      .WithMany()
                      .HasForeignKey(b => b.TenantID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblPayment>(entity =>
            {
                entity.ToTable("Payments");
                entity.HasKey(e => e.PaymentID);
                entity.Property(e => e.Method).HasMaxLength(50);
                entity.Property(e => e.Remarks).HasMaxLength(300);

                // deleting a bill removes its payment rows too
                entity.HasOne(p => p.Billing)
                      .WithMany()
                      .HasForeignKey(p => p.BillingID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblMaintenanceRequest>(entity =>
            {
                entity.ToTable("MaintenanceRequests");
                entity.Property(e => e.AssignedStaffID).HasColumnName("AssignedStaffUserID");
                entity.HasKey(e => e.RequestID);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Priority).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
                entity.Property(e => e.StaffNotes).HasMaxLength(500);

                entity.HasOne(m => m.Tenant)
                      .WithMany()
                      .HasForeignKey(m => m.TenantID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblLostFoundItem>(entity =>
            {
                entity.ToTable("LostFoundItems");
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

                entity.HasOne(l => l.ClaimedBy)
                      .WithMany()
                      .HasForeignKey(l => l.ClaimedByUserID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblClaimRequest>(entity =>
            {
                entity.ToTable("ClaimRequests");
                entity.HasKey(e => e.ClaimID);
                entity.Property(e => e.VerificationDetails).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.HasOne(c => c.Item)
                      .WithMany()
                      .HasForeignKey(c => c.ItemID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Claimant)
                      .WithMany()
                      .HasForeignKey(c => c.ClaimantUserID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<tblUnitTransferRequest>(entity =>
            {
                entity.ToTable("UnitTransferRequests");
                entity.HasKey(e => e.TransferID);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.HasOne(r => r.Tenant)
                      .WithMany()
                      .HasForeignKey(r => r.TenantID)
                      .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<TenantUnitAssignment>(entity =>
            {
                entity.ToTable("TenantUnitAssignments");
                entity.HasKey(e => e.AssignmentID);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property<int?>("ActiveTenantID")
                    .HasComputedColumnSql("CASE WHEN `Status` = 'Active' THEN `TenantID` ELSE NULL END", stored: true);
                entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantID).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Unit).WithMany().HasForeignKey(e => e.UnitID).OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}
