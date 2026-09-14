using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models
{
    // Every account that can sign in, whatever its role.
    //
    // Two things moved here from the old tenant row: the person's name and their
    // contact number. A tenant is a person with an account, not a separate kind
    // of thing, so their identity lives with the account and only the tenancy —
    // the money and the unit history — hangs off TenantProfile.
    public class User
    {
        public int UserID { get; set; }

        public int RoleID { get; set; }

        [Required, StringLength(50)]
        public string Username { get; set; } = string.Empty;

        // PBKDF2-SHA256, 100k iterations, 16-byte salt — see PasswordHelper.
        // Named for what it holds: this is never the password itself.
        [Required, StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(30)]
        public string? ContactNumber { get; set; }

        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; } = true;

        // the one account that cannot be deleted or deactivated
        public bool IsMainAdmin { get; set; }

        public DateTime? LastLoginAt { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public DateTime DateUpdated { get; set; } = DateTime.Now;

        public Role? Role { get; set; }
        public TenantProfile? TenantProfile { get; set; }

        // "Bayotas, Clark" is how the design writes a person in a list
        public string FullName =>
            string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
                ? Username
                : $"{FirstName} {LastName}".Trim();

        public string SortableName => $"{LastName}, {FirstName}".Trim(' ', ',');

        // Shown in lists instead of the login username, so the admin can tell at
        // a glance WHO made a report or request.
        public string DisplayName => FullName;

        public string RoleName => Role?.RoleName ?? string.Empty;
    }
}
