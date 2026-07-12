using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class TenantViewModel
    {
        public int TenantID { get; set; }

        public int? UserID { get; set; }

        // login fields - required when creating, optional when editing
        [MaxLength(50)]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [MaxLength(255)]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
            ErrorMessage = "Password must be at least 8 characters with an uppercase letter, a lowercase letter, a number, and a special character.")]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        [Display(Name = "Unit")]
        public int UnitID { get; set; }

        [Required(ErrorMessage = "First Name is required.")]
        [MaxLength(50)]
        [RegularExpression(@"^[A-Za-zñÑ .'-]+$", ErrorMessage = "First Name may not contain numbers.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        [MaxLength(50)]
        [RegularExpression(@"^[A-Za-zñÑ .'-]+$", ErrorMessage = "Last Name may not contain numbers.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Contact Number must contain digits only (7–15 digits).")]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Emergency Contact must contain digits only (7–15 digits).")]
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
