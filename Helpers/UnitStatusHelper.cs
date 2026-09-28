using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.Helpers
{
    // Re-derives a unit's status from occupancy and any OPEN unit requests:
    //   Under Maintenance : left as-is (set manually by an admin)
    //   Occupied          : active tenants have filled the unit
    //   Reserved          : has room, but a tenant has an OPEN application/transfer to it
    //   Available            : has room and no open request
    // The caller is responsible for SaveChanges.
    public static class UnitStatusHelper
    {
        // A request holds the unit while it is Pending OR Approved. Approved means
        // the admin agreed but the tenant has not moved yet, so the unit must stay
        // Reserved for them — otherwise somebody else could request it in between.
        public static readonly string[] OpenTransferStatuses = { "Pending", "Approved" };


        public static async Task RefreshAsync(ApplicationDbContext db, int? unitId)
        {
            if (unitId == null) return;
            var unit = await db.tblUnits.FindAsync(unitId.Value);
            if (unit == null || unit.Status == "Under Maintenance") return;

            int active = await db.tblTenants.CountAsync(t => t.Assignments.Any(a => a.Status == "Active" && a.UnitID == unitId) && t.Status == "Active");
            if (active >= unit.Capacity)
            {
                unit.Status = "Occupied";
                return;
            }

            bool reserved = await db.tblUnitTransferRequests
                .AnyAsync(r => r.RequestedUnitID == unitId &&
                               (r.Status == "Pending" || r.Status == "Approved"));
            unit.Status = reserved ? "Reserved" : "Available";
        }
    }
}
