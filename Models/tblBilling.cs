using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoAMS.Models
{
    public class tblBilling
    {
        [Key]
        public int BillingID { get; set; }

        [Required]
        public int TenantID { get; set; }

        [Required]
        [Display(Name = "Billing Period")]
        public DateTime BillingPeriod { get; set; }   // stored as first day of month

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount Due")]
        public decimal AmountDue { get; set; }

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
