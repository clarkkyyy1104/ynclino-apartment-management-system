using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblLostFoundItem
    {
        [Key]
        public int ItemID { get; set; }

        [Required]
        public int ReportedByUserID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required, MaxLength(10)]
        [Display(Name = "Type")]
        public string ItemType { get; set; } = "Lost";   // Lost | Found

        [MaxLength(200)]
        public string? Location { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Reported";   // Reported | Claimed (final)

        [Display(Name = "Date Reported")]
        public DateTime DateReported { get; set; } = DateTime.Now;

        // Who ended up with the item. Kept on the ITEM, not only on the claim
        // request, because an admin can also hand an item back over the counter
        // and mark it Claimed without any online claim being filed.
        [Display(Name = "Claimed By")]
        public int? ClaimedByUserID { get; set; }

        [Display(Name = "Date Claimed")]
        public DateTime? DateClaimed { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(260)]
        [Display(Name = "Photo")]
        public string? ImagePath { get; set; }

        [ForeignKey("ReportedByUserID")]
        public tblUser? ReportedBy { get; set; }

        [ForeignKey("ClaimedByUserID")]
        public tblUser? ClaimedBy { get; set; }
    }
}
