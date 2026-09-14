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
        
        // the same units, named and explained rather than only counted
        public List<UnitStateRow> UnitStates { get; set; } = new List<UnitStateRow>();
        public decimal RentBeingEarned { get; set; }
        public decimal RentNotBeingEarned { get; set; }

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

        // every tenant and where their account stands
        public List<TenantBalanceRow> TenantAccounts { get; set; } = new List<TenantBalanceRow>();

        // just the ones who owe, kept for the headline figures
        public List<TenantBalanceRow> Outstanding => TenantAccounts.Where(x => x.Balance > 0).ToList();
                public List<MaintenanceCountRow> MaintenanceByStatus { get; set; } = new List<MaintenanceCountRow>();
        public List<MaintenanceCategoryRow> MaintenanceByCategory { get; set; } = new List<MaintenanceCategoryRow>();
        public List<TenantHistoryRow> TenantHistory { get; set; } = new List<TenantHistoryRow>();
    }
    // One state a unit can be in — but written the way the owner would say it,
    // and naming the actual units, because "Available: 5" does not tell anyone
    // WHICH five to go and advertise.
    public class UnitStateRow
    {
        public string State { get; set; } = string.Empty;      // "Empty and ready"
        public string Meaning { get; set; } = string.Empty;    // what it means for the owner
        public List<string> UnitNumbers { get; set; } = new List<string>();
        public decimal MonthlyRent { get; set; }
        public int Count => UnitNumbers.Count;
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

        // shown so the row can be checked rather than taken on trust:
        // Billed − Paid must equal Balance, on every line
        public decimal AdvanceCredit { get; set; }
        public DateTime? LastPaymentDate { get; set; }
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

    // One tenant's account, as the standing-balance report shows it. Every
    // tenant gets a row, including the ones who owe nothing and the ones who
    // have never been billed — a report that only lists debtors cannot be used
    // to check that the tenants who are square really are square.
    public class TenantBalanceRow
    {
        public int TenantID { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? UnitNumber { get; set; }
        public string Status { get; set; } = string.Empty;

        public int BillsIssued { get; set; }
        public decimal Billed { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public int OverdueBills { get; set; }

        // money the tenant has handed over that no bill has claimed yet
        public decimal AdvanceCredit { get; set; }

        // when they last paid anything, and how much
        public DateTime? LastPaymentDate { get; set; }
        public decimal LastPaymentAmount { get; set; }
    }

    public class MaintenanceCountRow
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}