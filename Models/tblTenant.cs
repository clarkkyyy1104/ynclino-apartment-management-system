using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoAMS.Models
{
    public class tblTenant
    {
        [Key]
        public int TenantID { get; set; }

        public int? UserID { get; set; }

        [Required]
        public int UnitID { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

        [Display(Name = "Move-In Date")]
        public DateTime? MoveInDate { get; set; }

        [Display(Name = "Move-Out Date")]
        public DateTime? MoveOutDate { get; set; }

        [Display(Name = "Lease Start")]
        public DateTime? LeaseStart { get; set; }

        [Display(Name = "Lease End")]
        public DateTime? LeaseEnd { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active | Inactive

        [Display(Name = "Date Recorded")]
        public DateTime DateRecorded { get; set; } = DateTime.Now;

        [ForeignKey("UserID")]
        public tblUser? User { get; set; }

        [ForeignKey("UnitID")]
        public tblUnit? Unit { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}
