using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
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
        [Display(Name = "Amount Due")]
        public decimal AmountDue { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Paid")]
        public decimal? AmountPaid { get; set; }

        [Display(Name = "Date Paid")]
        public DateTime? DatePaid { get; set; }

        // how the payment was made (recorded when a payment is entered)
        [MaxLength(20)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }   // Cash | GCash

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Unpaid";   // Unpaid | Partial | Paid

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Date Issued")]
        public DateTime DateIssued { get; set; } = DateTime.Now;

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }
    }
}
