using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models
{
    // Admin | Maintenance | Tenant.
    //
    // The role used to be a string column on the user row. It is a table of its
    // own now, so a typo cannot invent a fourth role and the set of roles is
    // something you can read off the database rather than out of the C#.
    public class Role
    {
        public int RoleID { get; set; }

        [Required, StringLength(50)]
        public string RoleName { get; set; } = string.Empty;

        public ICollection<User> Users { get; set; } = new List<User>();

        // the three names the application checks against, in one place
        public const string Admin = "Admin";
        public const string Maintenance = "Maintenance";
        public const string Tenant = "Tenant";
    }
}
