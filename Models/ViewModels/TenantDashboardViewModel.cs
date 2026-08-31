namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class TenantDashboardViewModel
    {
        public tblTenant? Tenant { get; set; }
        public List<tblNotification> Unread { get; set; } = new();
        public int BillingCount { get; set; }
        public int MaintenanceCount { get; set; }
        public int LostFoundCount { get; set; }
        public int TransferCount { get; set; }

        public int TotalUnread => Unread.Count;

        // the tenant's outstanding balance across every unpaid bill
        public decimal Outstanding {get; set; }
        public decimal AdvanceCredit {get; set; }
    }
}