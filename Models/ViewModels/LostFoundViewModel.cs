using System.ComponentModel.DataAnnotations;

namespace YnclinoAMS.Models.ViewModels
{
    public class LostFoundViewModel
    {
        public int ItemID { get; set; }

        public int ReportedByUserID { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        [MaxLength(100)]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        [Display(Name = "Type")]
        public string ItemType { get; set; } = "Lost";

        [MaxLength(200)]
        [Display(Name = "Last Known Location")]
        public string? Location { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Reported";

        [MaxLength(500)]
        [Display(Name = "Additional Notes")]
        public string? Notes { get; set; }

        // Display helpers
        public string? ReportedByName { get; set; }
        public DateTime DateReported { get; set; } = DateTime.Now;
    }
}
