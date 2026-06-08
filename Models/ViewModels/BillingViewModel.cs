using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YnclinoAMS.Models.ViewModels
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
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

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

        public IEnumerable<SelectListItem> AvailableTenants { get; set; } = new List<SelectListItem>();
    }
}
