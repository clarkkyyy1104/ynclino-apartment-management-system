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

        [Required, MaxLength(50)]
        public string Category { get; set; } = "Other";   // Plumbing | Electrical | Structural | Appliance | Other

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Priority { get; set; } = "Medium";   // Low | Medium | High

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";   // Pending | In Progress | Resolved | Cancelled

        [Display(Name = "Date Submitted")]
        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        [Display(Name = "Date Resolved")]
        public DateTime? DateResolved { get; set; }

        [MaxLength(500)]
        [Display(Name = "Admin Notes")]
        public string? AdminNotes { get; set; }

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }
    }
}
