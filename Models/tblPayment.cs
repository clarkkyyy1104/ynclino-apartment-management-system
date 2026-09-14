using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    // One row per payment actually received, so a bill can be settled over time
    // (e.g. 1,000 today + 2,000 next week + 3,000 later = 6,000 paid in full).
    // The bill's AmountPaid is kept as the running total of these rows.
    public class tblPayment
    {
        [Key]
        public int PaymentID { get; set; }

        // the bill this payment is applied to
        [Required]
        public int BillingID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        // when the tenant actually handed over the money (the admin may backdate it)
        [Required]
        [Display(Name = "Date Paid")]
        public DateTime DatePaid { get; set; } = DateTime.Today;

        [MaxLength(50)]
        [Display(Name = "Payment Method")]
        public string? Method { get; set; }      // Cash | GCash | Bank Transfer

        [MaxLength(300)]
        public string? Remarks { get; set; }

        // when the admin encoded it — audit trail, separate from DatePaid
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        [ForeignKey("BillingID")]
        public tblBilling? Billing { get; set; }
    }
}
