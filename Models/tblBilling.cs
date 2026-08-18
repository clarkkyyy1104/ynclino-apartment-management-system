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

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Paid")]
        public decimal? AmountPaid { get; set; }

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
