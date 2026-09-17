using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class EditUserInfoViewModel
    {
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        [RegularExpression(@"^[\p{L} .'-]+$", ErrorMessage = "First Name may not contain numbers.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [RegularExpression(@"^[\p{L} .'-]+$", ErrorMessage = "Last Name may not contain numbers.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Contact Number must contain 7 to 15 digits.")]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }
    }
}
