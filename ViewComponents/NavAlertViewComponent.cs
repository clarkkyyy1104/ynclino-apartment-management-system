using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.ViewComponents
{
    // Shows a small red dot on a navigation link when that module has
    // something waiting for the current user. Rendered from _Layout.
    public class NavAlertViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NavAlertViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            bool alert = module switch
            {
                "Billing"   => await HasBillingAlertAsync(),
                "LostFound" => await HasLostFoundAlertAsync(),
                "Transfer"  => await HasTransferAlertAsync(),
                _           => false
            };
            return View(alert);
        }

        private bool IsStaff() => User.IsInRole("Admin");

        private int? CurrentUserId() =>
            int.TryParse(((ClaimsPrincipal)User).FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // Staff: bills are overdue (or past due and not yet swept).
        // Tenant: you still owe on a bill.
        private async Task<bool> HasBillingAlertAsync()
        {
            if (IsStaff())
                return await _context.tblBillings.AnyAsync(b =>
                    b.Status == "Overdue" ||
                    (b.Status == "Unpaid" && b.DueDate < DateTime.Today));

            var uid = CurrentUserId();
            if (uid == null) return false;
            return await _context.tblBillings.AnyAsync(b =>
                b.Tenant!.UserID == uid &&
                (b.Status == "Unpaid" || b.Status == "Overdue"));
        }

        // Staff: there are claims waiting to be reviewed.
        // Tenant: a found item has been posted that you could claim.
        private async Task<bool> HasLostFoundAlertAsync()
        {
            if (IsStaff())
                return await _context.tblClaimRequests.AnyAsync(c => c.Status == "Pending");

            var uid = CurrentUserId();
            if (uid == null) return false;
            return await _context.tblLostFoundItems.AnyAsync(l =>
                l.ItemType == "Found" &&
                l.Status == "Reported" &&
                l.ReportedByUserID != uid);
        }

        // Staff: there are unit-transfer requests waiting to be reviewed.
        // Tenant: no alert (they can see their own request status on the page).
        private async Task<bool> HasTransferAlertAsync()
        {
            if (!IsStaff()) return false;
            return await _context.tblUnitTransferRequests.AnyAsync(r => r.Status == "Pending");
        }
    }
}
