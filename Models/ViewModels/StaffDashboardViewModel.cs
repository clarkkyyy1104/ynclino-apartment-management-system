namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // What a maintenance staff member sees when they sign in: only the work
    // assigned to them, with the urgent items counted separately.
    public class StaffDashboardViewModel
    {
        public string StaffName { get; set; } = string.Empty;

        // requests assigned to this staff member that are not finished yet
        public List<tblMaintenanceRequest> MyOpenRequests { get; set; } = new();

        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int UrgentCount { get; set; }

        // finished work, for a sense of progress
        public int ResolvedCount { get; set; }
    }
}