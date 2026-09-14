namespace YnclinoApartmentManagementSystem.Models
{
    public class MaintenanceRequest
    {
        public int RequestID { get; set; }

        public int TenantID { get; set; }

        public int? UnitID { get; set; }

        // The staff member handling it. This points at an account, not at a
        // separate staff table, so the application has to check the account
        // actually holds the Maintenance role before assigning it.
        public int? AssignedStaffUserID { get; set; }

        public string Category { get; set; } = "Other";   // Plumbing | Electrical | Structural | Appliance | Other

        public string Description { get; set; } = string.Empty;

        public string Priority { get; set; } = "Moderate";   // Minor | Moderate | Major | Urgent

        public string Status { get; set; } = "Pending";   // Pending | In Progress | Resolved | Cancelled

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        public DateTime? DateResolved { get; set; }

        // filed away per side: the tenant's copy and the office's copy
        public DateTime? TenantArchivedAt { get; set; }
        public DateTime? StaffArchivedAt { get; set; }

        public string? StaffNotes { get; set; }

        public string? ImagePath { get; set; }

        public TenantProfile? Tenant { get; set; }
        public Unit? Unit { get; set; }
        public User? AssignedStaff { get; set; }
    }
}
