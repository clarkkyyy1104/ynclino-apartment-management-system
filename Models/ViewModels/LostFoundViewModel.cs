using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
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

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public string ItemType { get; set; } = "";

        [MaxLength(200)]
        [Display(Name = "Last Known Location")]
        public string? Location { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Reported";

        [MaxLength(500)]
        [Display(Name = "Additional Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Photo (optional)")]
        public IFormFile? ImageUpload { get; set; }

        // for display only
        public string? ReportedByName { get; set; }

        // 0 = nobody chosen yet. Required once the status is set to "Claimed".
        [Display(Name = "Claimed By")]
        public int ClaimedByUserID { get; set; }
        public string? ClaimedByName { get; set; }
        public DateTime? DateClaimed { get; set; }
        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableTenants { get; set; }
            = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        public DateTime DateReported { get; set; } = DateTime.Now;
        public string? ImagePath { get; set; }
    }
}
