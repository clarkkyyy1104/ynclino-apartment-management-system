using System.ComponentModel.DataAnnotations;

namespace YnclinoAMS.Models.ViewModels
{
    public class UserViewModel
    {
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [MaxLength(255)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string? ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = "Tenant";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
