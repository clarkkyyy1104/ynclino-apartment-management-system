namespace YnclinoApartmentManagementSystem.Models.ViewModels
{
    // Everything the Reports & Records dashboard needs, gathered in one object.
    public class ReportsViewModel
    {
        // ── Units / occupancy ──
        public int TotalUnits { get; set; }
        public int OccupiedUnits { get; set; }
        public int AvailableUnits { get; set; }
        public int ReservedUnits { get; set; }
        public int MaintenanceUnits { get; set; }

        // percentage of units currently occupied
        public double OccupancyRate =>
            TotalUnits == 0 ? 0 : (double)OccupiedUnits / TotalUnits * 100;

        // ── Tenants ──
        public int ActiveTenants { get; set; }
        public int InactiveTenants { get; set; }

        // ── Money ──
        public decimal IncomeThisMonth { get; set; }
        public decimal IncomeAllTime { get; set; }
        public decimal OutstandingTotal { get; set; }
        public int OverdueTenants { get; set; }

        // ── Open requests ──
        public int PendingMaintenance { get; set; }
        public int PendingTransfers { get; set; }
        public int OpenLostFound { get; set; }

        // ── Tables ──
        public List<MonthlyIncomeRow> MonthlyIncome { get; set; } = new List<MonthlyIncomeRow>();
        public List<TenantBalanceRow> Outstanding { get; set; } = new List<TenantBalanceRow>();
                public List<MaintenanceCountRow> MaintenanceByStatus { get; set; } = new List<MaintenanceCountRow>();
        public List<MaintenanceCategoryRow> MaintenanceByCategory { get; set; } = new List<MaintenanceCategoryRow>();
        public List<TenantHistoryRow> TenantHistory { get; set; } = new List<TenantHistoryRow>();
    }

    // one row of the maintenance report, grouped by issue type
    public class MaintenanceCategoryRow
    {
        public string Category { get; set; } = string.Empty;
        public int Requests { get; set; }
    }

    // one row of the "tenant histories" report
    public class TenantHistoryRow
    {
        public int TenantID { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? UnitNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public int MonthsBilled { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; }
        public int MaintenanceRequests { get; set; }
        public int TransferRequests { get; set; }
    }

    public class MonthlyIncomeRow
    {
        public DateTime Month { get; set; }
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
        public decimal Uncollected => Billed - Collected;
    }

    public class TenantBalanceRow
    {
        public int TenantID { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? UnitNumber { get; set; }
        public decimal Billed { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public int OverdueBills { get; set; }
    }

    public class MaintenanceCountRow
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}