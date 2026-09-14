namespace YnclinoApartmentManagementSystem.Models
{
    // One row per payment actually received, so a bill can be settled over time
    // (e.g. 1,000 today + 2,000 next week + 3,000 later = 6,000 paid in full).
    // These rows are the only record of money received; a bill's paid total is
    // their sum.
    public class Payment
    {
        public int PaymentID { get; set; }

        // the bill this payment is applied to
        public int BillingID { get; set; }

        public decimal Amount { get; set; }

        // when the tenant actually handed over the money (the admin may backdate it)
        public DateTime DatePaid { get; set; } = DateTime.Today;

        public string? Method { get; set; }      // Cash | GCash | Bank Transfer

        public string? Remarks { get; set; }

        // when the admin encoded it — audit trail, separate from DatePaid
        public DateTime RecordedAt { get; set; } = DateTime.Now;

        public Billing? Billing { get; set; }
    }
}
