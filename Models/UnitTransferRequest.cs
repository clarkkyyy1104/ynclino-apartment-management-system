namespace YnclinoApartmentManagementSystem.Models
{
    public class UnitTransferRequest
    {
        public int TransferID { get; set; }

        public int TenantID { get; set; }

        // where they were when they asked. Kept on the request itself: approving
        // it moves their assignment, and without this the "From" column could no
        // longer say where they came from.
        public int? CurrentUnitID { get; set; }

        public int RequestedUnitID { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected | Cancelled

        public DateTime DateRequested { get; set; } = DateTime.Now;
        public DateTime? DateReviewed { get; set; }

        public DateTime? TenantArchivedAt { get; set; }
        public DateTime? StaffArchivedAt { get; set; }

        public string? AdminNotes { get; set; }

        public TenantProfile? Tenant { get; set; }
        public Unit? CurrentUnit { get; set; }
        public Unit? RequestedUnit { get; set; }
    }
}
