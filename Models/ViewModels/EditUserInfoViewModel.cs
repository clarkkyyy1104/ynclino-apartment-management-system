using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class EditUserInfoViewModel
    {
        public int UserID { get; set; }

        // Same pattern as UserViewModel and TenantViewModel. This used to be
        // ^[\p{L} .'-]+$, which works on the server but not in the browser:
        // jQuery validation builds the pattern with new RegExp() and no "u"
        // flag, so \p{L} there means the literal characters p { L } — every
        // real name ("maria", "Staff") was rejected before it was ever sent.
        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-zñÑ .'-]+$", ErrorMessage = "First Name may not contain numbers.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-zñÑ .'-]+$", ErrorMessage = "Last Name may not contain numbers.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        [RegularExpression(@"^[0-9]{7,15}$", ErrorMessage = "Contact Number must contain 7 to 15 digits.")]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }
    }
}
