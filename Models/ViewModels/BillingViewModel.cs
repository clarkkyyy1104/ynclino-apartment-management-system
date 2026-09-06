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

        [Required(ErrorMessage = "Amount Paid is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        [Display(Name = "Amount Paid")]
        public decimal AmountPaid { get; set; }

        [MaxLength(20)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // for display only
        public string? TenantName { get; set; }

        // used by the Update (top-up a partial payment) screen
        [Display(Name = "Monthly Rent")]
        public decimal TenantRent { get; set; }

        [Display(Name = "Already Paid")]
        public decimal AlreadyPaid { get; set; }

        [Display(Name = "Remaining Balance")]
        public decimal RemainingBalance { get; set; }

        public IEnumerable<SelectListItem> AvailableTenants { get; set; } = new List<SelectListItem>();
    }
}
