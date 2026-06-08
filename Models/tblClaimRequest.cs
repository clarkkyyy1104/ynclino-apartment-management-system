using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblClaimRequest
    {
        [Key]
        public int ClaimID { get; set; }

        [Required]
        public int ItemID { get; set; }

        [Required]
        public int ClaimantUserID { get; set; }

        [Required, MaxLength(1000)]
        [Display(Name = "Proof of Ownership")]
        public string VerificationDetails { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected

        [MaxLength(500)]
        public string? AdminNotes { get; set; }

        [ForeignKey("ItemID")]
        public tblLostFoundItem? Item { get; set; }

        [ForeignKey("ClaimantUserID")]
        public tblUser? Claimant { get; set; }
    }
}
