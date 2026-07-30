using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblUnitTransferRequest
    {
        [Key]
        public int TransferID { get; set; }

        [Required]
        public int TenantID { get; set; }

        // the unit the tenant is in when the request is filed
        [Required]
        public int CurrentUnitID { get; set; }

        // the vacant unit the tenant wants to move to
        [Required]
        public int RequestedUnitID { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected

        public DateTime DateRequested { get; set; } = DateTime.Now;

        public DateTime? DateReviewed { get; set; }

        [MaxLength(500)]
        public string? AdminNotes { get; set; }

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }

        [ForeignKey("CurrentUnitID")]
        public tblUnit? CurrentUnit { get; set; }

        [ForeignKey("RequestedUnitID")]
        public tblUnit? RequestedUnit { get; set; }
    }
}
