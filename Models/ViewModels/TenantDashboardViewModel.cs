namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    public class TenantDashboardViewModel
    {
        public TenantProfile? Tenant { get; set; }
        // what the records say still needs this tenant
        public List<SystemNotification> Notifications { get; set; } = new();
        public int TotalNotifications => Notifications.Count;

        // the tenant's outstanding balance across every unpaid bill
        public decimal Outstanding {get; set; }
        public decimal AdvanceCredit {get; set; }
    }
}