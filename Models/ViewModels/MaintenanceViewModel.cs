using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoAMS.Models.ViewModels
{
    public class MaintenanceViewModel
    {
        public int RequestID { get; set; }

        [Required(ErrorMessage = "Tenant is required.")]
        [Display(Name = "Tenant")]
        public int TenantID { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        [Display(Name = "Category")]
        public string Category { get; set; } = "Other";

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Priority is required.")]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Medium";

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

        // for display only
        public string? TenantName { get; set; }
        public string? UnitNumber { get; set; }

        public IEnumerable<SelectListItem> AvailableTenants { get; set; } = new List<SelectListItem>();
    }
}
