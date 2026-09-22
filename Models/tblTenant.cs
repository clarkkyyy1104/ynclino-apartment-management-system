using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblTenant
    {
        [Key]
        public int TenantID { get; set; }

        public int? UserID { get; set; }

        // UI compatibility only: the database stores occupancy in assignments.
        // A setter stages an assignment change; SaveChangesAsync persists it.
        private int? requestedUnitID;
        [NotMapped]
        public bool HasPendingUnitChange { get; private set; }
        [NotMapped]
        public int? UnitID
        {
            get => HasPendingUnitChange ? requestedUnitID : CurrentAssignment?.UnitID;
            set { requestedUnitID = value; HasPendingUnitChange = true; }
        }

        public ICollection<TenantUnitAssignment> Assignments { get; set; } = new List<TenantUnitAssignment>();
        [NotMapped]
        public TenantUnitAssignment? CurrentAssignment => Assignments.SingleOrDefault(a => a.Status == "Active");

        internal void AcceptUnitChange()
        {
            requestedUnitID = null;
            HasPendingUnitChange = false;
        }

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
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(50)]
        [Display(Name = "Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [MaxLength(20)]
        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        [NotMapped, Display(Name = "Move-In Date")]
        public DateTime? MoveInDate { get; set; }

        [NotMapped, Display(Name = "Move-Out Date")]
        public DateTime? MoveOutDate { get; set; }

        [NotMapped, Display(Name = "Lease Start")]
        public DateTime? LeaseStart { get; set; }

        [NotMapped, Display(Name = "Lease End")]
        public DateTime? LeaseEnd { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active | Inactive

        // Money paid beyond what a bill required. Carried forward and used
        // automatically on the tenant's next bill.
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Advance Payment")]
        public decimal AdvanceCredit { get; set; }

        [Display(Name = "Date Recorded")]
        public DateTime DateRecorded { get; set; } = DateTime.Now;

        // optional profile photo the tenant can upload
        [MaxLength(300)]
        public string? PhotoPath { get; set; }

        [ForeignKey("UserID")]
        public tblUser? User { get; set; }

        [NotMapped]
        public tblUnit? Unit => CurrentAssignment?.Unit;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}
