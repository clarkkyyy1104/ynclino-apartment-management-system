using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    // A bill is a record of a payment a tenant made for a given month.
    public class tblBilling
    {
        [Key]
        public int BillingID { get; set; }

        [Required]
        public int TenantID { get; set; }

        [Required]
        [Display(Name = "Billing Period")]
        public DateTime BillingPeriod { get; set; }   // first day of the month

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Paid")]
        public decimal AmountPaid { get; set; }

        // how the payment was made
        [MaxLength(20)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }   // Cash | GCash

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Date Recorded")]
        public DateTime DateIssued { get; set; } = DateTime.Now;

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }

        // status is derived from how much was paid against the tenant's own rent:
        //   Paid    – paid their full monthly rent (or more)
        //   Partial – paid some, but less than their rent
        //   Unpaid  – nothing paid
        // (requires the Tenant to be loaded; falls back sensibly if not)
        [NotMapped]
        public string PaymentStatus
        {
            get
            {
                decimal rent = Tenant?.MonthlyRent ?? 0m;
                if (AmountPaid <= 0m) return "Unpaid";
                if (rent <= 0m || AmountPaid >= rent) return "Paid";
                return "Partial";
            }
        }
    }
}
