using System.ComponentModel.DataAnnotations;

namespace YnclinoAMS.Models
{
    public class tblUser
    {
        [Key]
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = "Tenant"; // Admin | SemiAdmin | Tenant

        public bool IsActive { get; set; } = true;

        public bool IsSuperAdmin { get; set; } = false;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public ICollection<tblTenant> Tenants { get; set; } = new List<tblTenant>();
    }
}
