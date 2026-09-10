using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class BillingViewModel
    {
        public int BillingID { get; set; }

        [Required(ErrorMessage = "Tenant is required.")]
        [Display(Name = "Tenant")]
        public int TenantID { get; set; }

        [Required(ErrorMessage = "Billing Period is required.")]
        [Display(Name = "Billing Period")]
        [DataType(DataType.Date)]
        public DateTime BillingPeriod { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        [Required(ErrorMessage = "Amount Due is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        [Display(Name = "Amount Due")]
        public decimal AmountDue { get; set; }

        [Required(ErrorMessage = "Due Date is required.")]
        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        // Rent for a month is due at the end of that month, so the starting value
        // matches the month above it rather than sitting 30 days from today — a
        // September bill was defaulting to 10 October.
        public DateTime DueDate { get; set; } =
            new DateTime(DateTime.Today.Year, DateTime.Today.Month,
                         DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));

        [Range(0, double.MaxValue)]
        [Display(Name = "Amount Paid")]
        public decimal? AmountPaid { get; set; }

        [Display(Name = "Date Paid")]
        [DataType(DataType.Date)]
        public DateTime? DatePaid { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Unpaid";

        [MaxLength(500)]
        public string? Notes { get; set; }

        // for display only
        public string? TenantName { get; set; }
        public string? UnitNumber { get; set; }

        // ── Recording a NEW payment ──────────────────────────────────────────
        // The admin types only what the tenant just handed over; the system adds it
        // to whatever was paid before. Leave blank to edit the bill without paying.
        [Range(0.01, double.MaxValue, ErrorMessage = "Payment must be greater than 0.")]
        [Display(Name = "Payment Amount")]
        public decimal? PaymentAmount { get; set; }

        [Display(Name = "Date Paid")]
        [DataType(DataType.Date)]
        public DateTime? PaymentDate { get; set; } = DateTime.Today;

        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [MaxLength(300)]
        [Display(Name = "Remarks")]
        public string? PaymentRemarks { get; set; }

        // running totals shown on the form (never typed by the admin)
        // shown read-only on the Update form so the deposit stays visible
        public decimal Deposit { get; set; }
        public decimal Advance { get; set; }

        // advance payment the tenant is holding (shown read-only)
        public decimal AdvanceCredit { get; set; }

        public decimal TotalPaid { get; set; }
        public decimal Balance => AmountDue - TotalPaid;

        // the part of a payment made here that was more than the bill asked for and
        // went to the tenant's advance payment instead
        public decimal AdvanceFromOverpayment { get; set; }

        // everything actually handed over against this bill
        public decimal TotalReceived => TotalPaid + AdvanceFromOverpayment;

        // this bill's payments, newest first
        public List<tblPayment> Payments { get; set; } = new List<tblPayment>();

        public IEnumerable<SelectListItem> AvailableTenants { get; set; } = new List<SelectListItem>();
    }

    // ── One tenant's billing record, a line per BILL ─────────────────────────
    // This page used to be built from the payments table, so a bill nobody had
    // paid produced no line at all: a tenant with seven bills and ₱9,000 owing
    // showed six rows and ₱3,000, and the report and the record disagreed. The
    // record is the bills now — the unpaid ones are exactly the rows that
    // matter — and the payments hang off the bill they settled.
    public class TenantLedgerRow
    {
        public int BillingID { get; set; }
        public DateTime BillingPeriod { get; set; }
        public DateTime DueDate { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance => AmountDue - AmountPaid;
        public string Status { get; set; } = string.Empty;
        public bool IssuedFromAdvance { get; set; }

        public List<tblPayment> Payments { get; set; } = new List<tblPayment>();

        public DateTime? FirstPaymentDate => Payments.Count == 0
            ? null : Payments.Min(p => p.DatePaid);
    }
}
