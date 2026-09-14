using System.ComponentModel.DataAnnotations;

namespace YnclinoApartmentManagementSystem.Models
{
    public class Unit
    {
        public int UnitID { get; set; }

        [Required, StringLength(20), Display(Name = "Unit Number")]
        public string UnitNumber { get; set; } = string.Empty;

        [Required, StringLength(50), Display(Name = "Unit Type")]
        public string UnitType { get; set; } = string.Empty;

        [Display(Name = "Rent Price")]
        public decimal RentPrice { get; set; }

        public decimal Deposit { get; set; }

        [Display(Name = "One Month Advance")]
        public decimal AdvancePayment { get; set; }

        public int Capacity { get; set; }

        public string Status { get; set; } = "Available"; // Available | Reserved | Occupied | Under Maintenance

        public DateTime DateAdded { get; set; } = DateTime.Now;
        public DateTime DateUpdated { get; set; } = DateTime.Now;

        // every stay this unit has ever had, past and present
        public ICollection<TenantUnitAssignment> Assignments { get; set; } = new List<TenantUnitAssignment>();
    }
}
