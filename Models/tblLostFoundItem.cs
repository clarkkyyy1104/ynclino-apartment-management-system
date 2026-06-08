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
        public string Status { get; set; } = "Reported";   // Reported | Claimed | Resolved

        [Display(Name = "Date Reported")]
        public DateTime DateReported { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [ForeignKey("ReportedByUserID")]
        public tblUser? ReportedBy { get; set; }
    }
}
