using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
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
        public string Role { get; set; } = "Tenant"; // Admin | Tenant

        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; }

        public bool IsMainAdmin { get; set; } = false;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public ICollection<tblTenant> Tenants { get; set; } = new List<tblTenant>();

        // Shown in lists instead of the login username, so the admin can tell at a
        // glance WHO made a report or request. Falls back to the username for
        // accounts that are not tenants (e.g. the administrator).
        [NotMapped]
        public string DisplayName =>
            Tenants != null && Tenants.Count > 0
                ? Tenants.First().FullName
                : Username;
    }
}
