using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class TenantViewModel
    {
        public int TenantID { get; set; }

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

        [MaxLength(100)]
        [RegularExpression(@"^[A-Za-zñÑ .'-]+$", ErrorMessage = "Emergency Contact Name may not contain numbers.")]
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

        public string FullName => $"{FirstName} {LastName}";
    }
}
