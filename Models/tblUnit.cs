using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblUnit
    {
        [Key]
        public int UnitID { get; set; }

        [Required, MaxLength(20)]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [Display(Name = "Unit Type")]
        public string UnitType { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Rent Price")]
        public decimal RentPrice { get; set; }

        // 1 month advance + 1 month deposit — defaults to twice the rent
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Deposit")]
        public decimal Deposit { get; set; }

        [Required]
        public int Capacity { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Vacant"; // Vacant | Occupied | Under Maintenance

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        public ICollection<tblTenant> Tenants { get; set; } = new List<tblTenant>();
    }
}
