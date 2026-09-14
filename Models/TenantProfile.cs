using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    // The tenancy side of a tenant: their money and their status. Their name and
    // contact number live on the User account; which unit they are in lives in
    // TenantUnitAssignments, one row per stay, so a transfer adds history
    // instead of overwriting it.
    public class TenantProfile
    {
        public int TenantID { get; set; }

        public int UserID { get; set; }

        // Money paid beyond what a bill required. Carried forward and used
        // automatically on the tenant's next bill.
        public decimal AdvanceCredit { get; set; }

        public string Status { get; set; } = "Active"; // Active | Inactive

        // Who to call if something happens to them. Kept here rather than on the
        // account because only tenants have one.
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactNumber { get; set; }

        public DateTime DateRecorded { get; set; } = DateTime.Now;
        public DateTime DateUpdated { get; set; } = DateTime.Now;

        public User? User { get; set; }
        public ICollection<TenantUnitAssignment> Assignments { get; set; } = new List<TenantUnitAssignment>();

        // ── Reading through to the account and the current stay ──────────────
        // These are conveniences for views. They are NOT mapped columns, so they
        // cannot appear inside a LINQ query that EF has to turn into SQL —
        // include the navigation and filter on the real column instead.

        [NotMapped] public string FullName => User?.FullName ?? string.Empty;
        [NotMapped] public string SortableName => User?.SortableName ?? string.Empty;
        [NotMapped] public string FirstName => User?.FirstName ?? string.Empty;
        [NotMapped] public string LastName => User?.LastName ?? string.Empty;
        [NotMapped] public string? ContactNumber => User?.ContactNumber;

        // The stay they are in right now, if any. A tenant has at most one
        // Active assignment — the database enforces that with a generated column.
        [NotMapped]
        public TenantUnitAssignment? CurrentAssignment =>
            Assignments?.FirstOrDefault(a => a.Status == "Active");

        [NotMapped] public Unit? Unit => CurrentAssignment?.Unit;
        [NotMapped] public int? UnitID => CurrentAssignment?.UnitID;
        [NotMapped] public DateTime? MoveInDate => CurrentAssignment?.MoveInDate;
        [NotMapped] public DateTime? MoveOutDate => CurrentAssignment?.MoveOutDate;
        [NotMapped] public DateTime? LeaseStart => CurrentAssignment?.LeaseStart;
        [NotMapped] public DateTime? LeaseEnd => CurrentAssignment?.LeaseEnd;
    }
}
