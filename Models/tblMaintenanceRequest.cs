using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblMaintenanceRequest
    {
        [Key]
        public int RequestID { get; set; }

        [Required]
        public int TenantID { get; set; }

        // The unit the repair belongs to. Copied from the tenant's unit when the
        // request is made, and then kept forever — so the repair history stays with
        // the APARTMENT even after that tenant transfers or moves out.
        [Display(Name = "Unit")]
        public int? UnitID { get; set; }

        // Which maintenance staff account is handling this request.
        [Display(Name = "Assigned Staff")]
        public int? AssignedStaffID { get; set; }

        [Required, MaxLength(50)]
        public string Category { get; set; } = "Other";   // Plumbing | Electrical | Structural | Appliance | Other

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Priority { get; set; } = "Moderate";   // Minor | Moderate | Major | Urgent

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";   // Pending | In Progress | Resolved | Cancelled

        [Display(Name = "Date Submitted")]
        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        [Display(Name = "Date Resolved")]
        public DateTime? DateResolved { get; set; }

        // A finished record stays in the ACTIVE list until somebody chooses to file
        // it away. Each side archives independently: the tenant clearing their own
        // list does not touch what the admin and maintenance staff see, and the
        // other way round. Null means "still in my active list".
        [Display(Name = "Archived by Tenant")]
        public DateTime? TenantArchivedAt { get; set; }

        [Display(Name = "Archived by Staff")]
        public DateTime? StaffArchivedAt { get; set; }

        // What the maintenance staff reported after doing the work. Kept separate
        // from AdminNotes so neither one overwrites the other.
        [MaxLength(500)]
        [Display(Name = "Work Notes")]
        public string? StaffNotes { get; set; }


        [MaxLength(260)]
        [Display(Name = "Photo")]
        public string? ImagePath { get; set; }

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }

        [ForeignKey("UnitID")]
        public tblUnit? Unit { get; set; }

        [ForeignKey("AssignedStaffID")]
        public tblUser? AssignedStaff { get; set; }
    }
}
