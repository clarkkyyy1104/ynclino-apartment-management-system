using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class MaintenanceViewModel
    {
        public int RequestID { get; set; }

        [Required(ErrorMessage = "Tenant is required.")]
        [Display(Name = "Tenant")]
        public int TenantID { get; set; }

        [Required(ErrorMessage = "Issue type is required.")]
        [Display(Name = "Issue Type")]
        public string Category { get; set; } = "Other";

        // nullable so an empty value isn't implicitly required; a description is
        // only enforced (in the controller) when the Issue Type is "Other"
        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Moderate";

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Date Submitted")]
        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        [Display(Name = "Date Resolved")]
        [DataType(DataType.Date)]
        public DateTime? DateResolved { get; set; }

        [MaxLength(500)]
        [Display(Name = "Admin Notes")]
        public string? AdminNotes { get; set; }

        [Display(Name = "Photo (optional)")]
        public IFormFile? ImageUpload { get; set; }

        // for display only
        public string? TenantName { get; set; }
        public string? UnitNumber { get; set; }
        public string? ImagePath { get; set; }

        public IEnumerable<SelectListItem> AvailableTenants { get; set; } = new List<SelectListItem>();
    }
}
