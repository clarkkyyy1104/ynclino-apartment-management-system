using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoAMS.Models.ViewModels
{
    public class UnitViewModel
    {
        public int UnitID { get; set; }

        [Required(ErrorMessage = "Unit Number is required.")]
        [MaxLength(20)]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Unit Type is required.")]
        [MaxLength(50)]
        [Display(Name = "Unit Type")]
        public string UnitType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rent Price is required.")]
        [Range(0.01, 999999.99, ErrorMessage = "Enter a valid rent price.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Rent Price")]
        public decimal RentPrice { get; set; }

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(20)]
        public string Status { get; set; } = "Vacant";

        public int TenantCount { get; set; }
    }
}
