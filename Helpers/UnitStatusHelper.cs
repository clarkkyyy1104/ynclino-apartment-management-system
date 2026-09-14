using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.Helpers
{
    // Re-derives a unit's status from occupancy and any pending unit requests:
    //   Under Maintenance : left as-is (set manually by an admin)
    //   Occupied          : active tenants have filled the unit
    //   Reserved          : has room, but a tenant has a PENDING application/transfer to it
    //   Available            : has room and no pending request
    // The caller is responsible for SaveChanges.
    public static class UnitStatusHelper
    {
        public static async Task RefreshAsync(ApplicationDbContext db, int? unitId)
        {
            if (unitId == null) return;
            var unit = await db.tblUnits.FindAsync(unitId.Value);
            if (unit == null || unit.Status == "Under Maintenance") return;

            int active = await db.tblTenants.CountAsync(t => t.UnitID == unitId && t.Status == "Active");
            if (active >= unit.Capacity)
            {
                unit.Status = "Occupied";
                return;
            }

            bool reserved = await db.tblUnitTransferRequests
                .AnyAsync(r => r.RequestedUnitID == unitId && r.Status == "Pending");
            unit.Status = reserved ? "Reserved" : "Available";
        }
    }
}
