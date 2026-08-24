using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class TenantViewModel
    {
        public int TenantID { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Range(0, 9999999.99, ErrorMessage = "Enter a valid rent amount.")]
        [Display(Name = "Monthly Rent")]
        public decimal MonthlyRent { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Contact Number must contain digits only (7–15 digits).")]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(50)]
        [Display(Name = "Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Emergency Contact Number must contain digits only (7–15 digits).")]
        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        [Display(Name = "Move-In Date")]
        [DataType(DataType.Date)]
        public DateTime? MoveInDate { get; set; }

        [Display(Name = "Move-Out Date")]
        [DataType(DataType.Date)]
        public DateTime? MoveOutDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        public string FullName => Name;
    }
}
