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

        // a move-in bill records how much of the total was deposit and advance.
        // for a normal monthly bill both are 0, so the whole AmountDue is rent.
        [Column(TypeName = "decimal(10,2)")]
        public decimal Deposit { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Advance { get; set; }

        [Required]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        // What the payment rows on this bill add up to — i.e. the part of the money
        // that settled THIS bill. It never exceeds AmountDue.
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Paid")]
        public decimal? AmountPaid { get; set; }

        // The part of a payment made against this bill that was MORE than the bill
        // asked for, and was therefore kept as advance payment for the next month.
        // Pay 12,000 on a 6,000 bill and this holds the extra 6,000, so the bill can
        // show where the whole 12,000 went instead of silently reporting only 6,000.
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Paid to Advance")]
        public decimal AdvanceFromOverpayment { get; set; }

        // Everything the tenant actually handed over against this bill.
        [NotMapped]
        [Display(Name = "Total Received")]
        public decimal TotalReceived => (AmountPaid ?? 0m) + AdvanceFromOverpayment;

        // True when the system issued this bill by itself because the tenant had
        // already paid for the month in advance. Nobody typed it in — it exists so the
        // month the overpayment covers is on record and cannot be billed twice.
        [Display(Name = "Issued from Advance Payment")]
        public bool IssuedFromAdvance { get; set; }

        [Display(Name = "Date Paid")]
        public DateTime? DatePaid { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Unpaid";   // Unpaid | Paid | Overdue

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Date Issued")]
        public DateTime DateIssued { get; set; } = DateTime.Now;

        [ForeignKey("TenantID")]
        public tblTenant? Tenant { get; set; }
    }
}
