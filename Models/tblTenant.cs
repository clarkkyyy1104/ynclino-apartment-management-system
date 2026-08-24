using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblTenant
    {
        [Key]
        public int TenantID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Monthly Rent")]
        public decimal MonthlyRent { get; set; }

        [MaxLength(20)]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(50)]
        [Display(Name = "Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [MaxLength(20)]
        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        [Display(Name = "Move-In Date")]
        public DateTime? MoveInDate { get; set; }

        [Display(Name = "Move-Out Date")]
        public DateTime? MoveOutDate { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active | Inactive

        [Display(Name = "Date Recorded")]
        public DateTime DateRecorded { get; set; } = DateTime.Now;

        // optional tenant photo the admin can attach
        [MaxLength(300)]
        public string? PhotoPath { get; set; }

        // kept as an alias so existing views that use FullName keep working
        [NotMapped]
        public string FullName => Name;
    }
}
