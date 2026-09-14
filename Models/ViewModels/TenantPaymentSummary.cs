namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // One row per tenant on the Payment History list, so the page stays readable.
    // The admin opens a tenant's Details to see every individual payment.
    public class TenantPaymentSummary
    {
        public int TenantID { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? UnitNumber { get; set; }
        public int PaymentCount { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}
