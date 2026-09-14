using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    // One month's bill for one tenant.
    //
    // There is no AmountPaid column any more. It used to be kept here as a
    // running total of the payment rows, which meant the same fact was written
    // in two places and could disagree — and it did, whenever a payment was
    // edited or deleted without the total being recalculated. Payments is the
    // only record of money received; what a bill has been paid is the sum of its
    // payments, worked out when asked.
    public class Billing
    {
        public int BillingID { get; set; }

        public int TenantID { get; set; }

        public DateTime BillingPeriod { get; set; }   // first day of the month

        public decimal AmountDue { get; set; }

        // charged at move-in, not every month
        public decimal Deposit { get; set; }
        public decimal Advance { get; set; }

        public DateTime DueDate { get; set; }

        // money that came in beyond this bill and was carried to the tenant's credit
        public decimal AdvanceFromOverpayment { get; set; }

        // true when this bill was raised out of credit the tenant already held
        public bool IssuedFromAdvance { get; set; }

        public DateTime? DatePaid { get; set; }

        public string Status { get; set; } = "Unpaid";   // Unpaid | Paid | Overdue

        public string? Notes { get; set; }

        public DateTime DateIssued { get; set; } = DateTime.Now;

        public DateTime? ArchivedAt { get; set; }

        public TenantProfile? Tenant { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // ── Worked out from the payments, never stored ───────────────────────
        // Only correct when Payments has been included. In a query that EF must
        // translate to SQL, sum the Payments navigation directly instead.

        [NotMapped] public decimal AmountPaid => Payments?.Sum(p => p.Amount) ?? 0m;
        [NotMapped] public decimal TotalReceived => AmountPaid + AdvanceFromOverpayment;
        [NotMapped] public decimal Balance => AmountDue - AmountPaid;
        [NotMapped] public bool IsSettled => Balance <= 0m;
    }
}
