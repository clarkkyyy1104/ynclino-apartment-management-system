using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // What a tenant may change about themselves from their own profile: their
    // name, their number and who to call. The unit, the rent and the login stay
    // with the office.
    public class ProfileInfoViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [RegularExpression(@"^\d{11}$", ErrorMessage = "Contact number must be 11 digits.")]
        [Display(Name = "Contact No.")]
        public string? ContactNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Emergency Contact")]
        public string? EmergencyContactName { get; set; }

        [StringLength(50)]
        [Display(Name = "Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [RegularExpression(@"^\d{11}$", ErrorMessage = "Emergency contact number must be 11 digits.")]
        [Display(Name = "Emergency Contact No.")]
        public string? EmergencyContactNumber { get; set; }
    }
}
