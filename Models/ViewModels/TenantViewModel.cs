using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoAMS.Models.ViewModels
{
    public class TenantViewModel
    {
        public int TenantID { get; set; }

        public int? UserID { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        [Display(Name = "Unit")]
        public int UnitID { get; set; }

        [Required(ErrorMessage = "First Name is required.")]
        [MaxLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        [MaxLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

        [Display(Name = "Move-In Date")]
        [DataType(DataType.Date)]
        public DateTime? MoveInDate { get; set; }

        [Display(Name = "Move-Out Date")]
        [DataType(DataType.Date)]
        public DateTime? MoveOutDate { get; set; }

        [Display(Name = "Lease Start")]
        [DataType(DataType.Date)]
        public DateTime? LeaseStart { get; set; }

        [Display(Name = "Lease End")]
        [DataType(DataType.Date)]
        public DateTime? LeaseEnd { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        public string FullName => $"{FirstName} {LastName}";

        public string? UnitNumber { get; set; }

        public IEnumerable<SelectListItem> AvailableUnits { get; set; } = new List<SelectListItem>();
    }
}
