using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models
{
    public class TenantUnitAssignment
    {
        [Key]
        public long AssignmentID { get; set; }
        public int TenantID { get; set; }
        public int UnitID { get; set; }
        public DateTime? MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public DateTime? LeaseStart { get; set; }
        public DateTime? LeaseEnd { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime DateRecorded { get; set; } = DateTime.Now;
        public DateTime DateUpdated { get; set; } = DateTime.Now;
        public tblTenant? Tenant { get; set; }
        public tblUnit? Unit { get; set; }
    }
}
