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

        // the unit the tenant is in when the request is filed; null when the tenant
        // has no unit yet and is applying for their first one
        public int? CurrentUnitID { get; set; }

        // the available unit the tenant wants to move to
        [Required]
        public int RequestedUnitID { get; set; }

        // optional — a tenant may submit a request without stating a reason
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        // A transfer is a process, not one event. Agreeing to a move and carrying
        // it out are separate steps, because days can pass between the decision and
        // the day the tenant actually moves.
        //   Pending   - submitted, nobody has decided yet
        //   Approved  - the admin agreed; SUBJECT FOR TRANSFER. The tenant has NOT
        //               moved, and the requested unit stays Reserved
        //   Completed - the admin confirmed the move happened; only now does the
        //               tenant's assignment change
        //   Rejected  - the admin refused it
        //   Cancelled - the tenant withdrew it, or an approved move was called off
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending | Approved | Completed | Rejected | Cancelled

        public DateTime DateRequested { get; set; } = DateTime.Now;

        // when the admin decided (approved or rejected)
        public DateTime? DateReviewed { get; set; }

        // when the tenant was actually moved. Null on every request that has not
        // reached Completed, which is what makes an unfinished transfer findable.
        [Display(Name = "Date Completed")]
        public DateTime? DateCompleted { get; set; }

        // A finished record stays in the ACTIVE list until somebody chooses to file
        // it away. Each side archives independently: the tenant clearing their own
        // list does not touch what the admin and maintenance staff see, and the
        // other way round. Null means "still in my active list".
        [Display(Name = "Archived by Tenant")]
        public DateTime? TenantArchivedAt { get; set; }

        [Display(Name = "Archived by Staff")]
        public DateTime? StaffArchivedAt { get; set; } 

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
