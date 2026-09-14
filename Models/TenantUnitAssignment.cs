namespace YnclinoApartmentManagementSystem.Models
{
    // One stay: this tenant, in this unit, between these dates.
    //
    // The old design kept a single UnitID on the tenant row, so approving a
    // transfer overwrote where they used to live and the history was gone. Every
    // assignment is kept now; a transfer ends one row and opens another.
    //
    // At most one row per tenant may be Active. The database enforces that
    // itself with a generated column (ActiveTenantID) under a UNIQUE key, so two
    // requests approved at the same moment cannot both succeed.
    public class TenantUnitAssignment
    {
        public int AssignmentID { get; set; }

        public int TenantID { get; set; }
        public int UnitID { get; set; }

        public DateTime? MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public DateTime? LeaseStart { get; set; }
        public DateTime? LeaseEnd { get; set; }

        public string Status { get; set; } = "Active"; // Active | Ended

        public DateTime DateRecorded { get; set; } = DateTime.Now;
        public DateTime DateUpdated { get; set; } = DateTime.Now;

        public TenantProfile? Tenant { get; set; }
        public Unit? Unit { get; set; }
    }
}
