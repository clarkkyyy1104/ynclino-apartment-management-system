using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

if (args.Contains("--check-localhost"))
{
    try { await LocalSchemaCheck.RunAsync(); }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Local read-only check failed: {error.GetType().Name}: {error.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    checks++;
}

// No application configuration or localhost connection is read by these tests.
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
await using var db = new ApplicationDbContext(options);
await db.Database.EnsureCreatedAsync();
Check(db.Model.FindEntityType(typeof(tblTenant))!.FindProperty("UnitID") == null,
    "TenantProfiles has no mapped UnitID column");

var first = new tblUnit { UnitNumber = "101", UnitType = "Studio", Capacity = 1, RentPrice = 2000 };
var second = new tblUnit { UnitNumber = "102", UnitType = "Studio", Capacity = 1, RentPrice = 2500 };
db.tblUnits.AddRange(first, second);
await db.SaveChangesAsync();
var tenant = new tblTenant {
    FirstName = "Test", LastName = "Tenant", UnitID = first.UnitID,
    User = new tblUser { Username = "assignment-test", Password = "test-only" }
};
db.tblTenants.Add(tenant);
await db.SaveChangesAsync();
int tenantId = tenant.TenantID;
Check(tenant.UnitID == first.UnitID && tenant.Assignments.Count == 1, "registration creates active assignment");
Check(first.Status == "Occupied", "registration updates occupancy");
db.ChangeTracker.Clear();
tenant = (await db.tblTenants.FindAsync(tenantId))!;
Check(tenant.Unit?.UnitNumber == "101", "FindAsync loads current unit through assignments");
Check((await db.tblTenants.AsNoTracking().FirstAsync()).Unit?.UnitNumber == "101", "untracked tenant query works");
Check((await db.tblUnits.Include(u => u.Assignments).ThenInclude(a => a.Tenant)
    .FirstAsync(u => u.UnitID == first.UnitID)).Tenants.Count == 1, "unit occupant list works");
Check((await db.tblUnits.AsNoTrackingWithIdentityResolution().Include(u => u.Assignments).ThenInclude(a => a.Tenant)
    .FirstAsync(u => u.UnitID == first.UnitID)).Tenants.Count == 1, "untracked unit graph uses identity resolution");
Check(await db.tblTenants.CountAsync(t => t.Status == "Active" &&
    t.Assignments.Any(a => a.Status == "Active" && a.UnitID == first.UnitID)) == 1,
    "occupancy filter translates to SQL");
Check(await db.tblUnits.Where(u => u.Assignments.Count(a => a.Status == "Active" && a.Tenant!.Status == "Active") < u.Capacity)
    .CountAsync() == 1, "available-unit filter translates to SQL");

tenant.UnitID = second.UnitID; // no mapped profile field changes
await db.SaveChangesAsync();
Check(tenant.UnitID == second.UnitID && tenant.Assignments.Count == 2 &&
    tenant.Assignments.Count(a => a.Status == "Ended") == 1, "unit-only transfer ends old row before inserting replacement");
Check(!tenant.HasPendingUnitChange, "pending unit change is cleared after successful save");
Check((await db.tblUnits.FindAsync(first.UnitID))!.Status == "Available" &&
    (await db.tblUnits.FindAsync(second.UnitID))!.Status == "Occupied", "transfer refreshes both units");
await db.SaveChangesAsync();
Check(await db.TenantUnitAssignments.CountAsync() == 2, "repeat save does not duplicate assignment");

db.tblBillings.Add(new tblBilling { TenantID = tenantId, AmountDue = 2500, BillingPeriod = DateTime.Today, DueDate = DateTime.Today });
db.tblMaintenanceRequests.Add(new tblMaintenanceRequest { TenantID = tenantId, UnitID = second.UnitID,
    Category = "Electrical", Description = "Test", Priority = "Minor" });
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Check((await db.tblBillings.AsNoTracking().Include(b => b.Tenant).ThenInclude(t => t!.Assignments)
    .ThenInclude(a => a.Unit).FirstAsync()).Tenant!.Unit!.RentPrice == 2500, "billing loads assigned rent");
Check((await db.tblUsers.Include(u => u.Tenants).FirstAsync()).Tenants.Single().UnitID == second.UnitID,
    "account query loads tenant current assignment");
var labels = await db.tblTenants.Where(t => t.Assignments.Any(a => a.Status == "Active"))
    .Select(t => new { Text = t.Assignments.Where(a => a.Status == "Active").Select(a => a.Unit!.UnitNumber).FirstOrDefault() })
    .ToListAsync();
Check(labels.Single().Text == "102", "tenant dropdown projection translates to SQL");
tenant = (await db.tblTenants.FindAsync(tenantId))!;
tenant.Status = "Inactive";
tenant.MoveOutDate = DateTime.Now;
await db.SaveChangesAsync();
Check(tenant.UnitID == null && tenant.Unit == null && tenant.Assignments.All(a => a.Status == "Ended"),
    "archive ends occupancy and never treats history as current");
Check((await db.tblUnits.FindAsync(second.UnitID))!.Status == "Available", "archive frees the previous unit");
Check((await db.tblMaintenanceRequests.Include(m => m.Unit).FirstAsync()).Unit!.UnitNumber == "102",
    "maintenance retains its historical unit snapshot");
await db.LoadTenantDatesAsync(tenant);
Check(tenant.MoveOutDate != null, "archived occupancy dates remain available");

Check(await db.PrepareTenantReactivationAsync(tenant) == null, "reactivation recovers last unit from history");
tenant.Status = "Active";
tenant.MoveOutDate = null;
await db.SaveChangesAsync();
Check(tenant.UnitID == second.UnitID && tenant.Assignments.Count == 3 && tenant.CurrentAssignment!.MoveOutDate == null,
    "reactivation creates a fresh active occupancy");

// An unavailable previous unit must not be silently overfilled on reactivation.
tenant.Status = "Inactive";
await db.SaveChangesAsync();
var occupant = new tblTenant { FirstName = "Other", LastName = "Tenant", UnitID = second.UnitID,
    User = new tblUser { Username = "other-test", Password = "test-only" } };
db.tblTenants.Add(occupant);
await db.SaveChangesAsync();
Check(await db.PrepareTenantReactivationAsync(tenant) != null && !tenant.HasPendingUnitChange,
    "reactivation refuses a now-full unit");

var unassigned = new tblTenant { FirstName = "New", LastName = "Tenant",
    User = new tblUser { Username = "unassigned-test", Password = "test-only" } };
db.tblTenants.Add(unassigned);
await db.SaveChangesAsync();
Check(unassigned.UnitID == null && unassigned.Assignments.Count == 0, "registration without unit remains supported");
unassigned.UnitID = first.UnitID;
await db.SaveChangesAsync();
Check(unassigned.CurrentAssignment?.UnitID == first.UnitID, "first-unit approval persists without profile column changes");
unassigned.UnitID = null;
await db.SaveChangesAsync();
Check(unassigned.CurrentAssignment == null, "explicit unassignment closes the active row");

db.TenantUnitAssignments.RemoveRange(unassigned.Assignments);
await db.SaveChangesAsync();
db.tblTenants.Remove(unassigned);
await db.SaveChangesAsync();
Check(!await db.tblTenants.AnyAsync(t => t.TenantID == unassigned.TenantID),
    "permanent deletion removes tracked assignment dependents before the tenant");

// Failure during the second save must roll back the ended old assignment too.
occupant.UnitID = int.MaxValue; // deliberately nonexistent unit, temporary DB only
bool rejected = false;
try { await db.SaveChangesAsync(); }
catch (DbUpdateException) { rejected = true; }
Check(rejected, "invalid replacement unit is rejected by the foreign key");
db.ChangeTracker.Clear();
occupant = (await db.tblTenants.FindAsync(occupant.TenantID))!;
Check(occupant.UnitID == second.UnitID && occupant.CurrentAssignment != null,
    "failed transfer rolls back closure of the original assignment");
db.TenantUnitAssignments.Add(new TenantUnitAssignment { TenantID = occupant.TenantID, UnitID = first.UnitID });
rejected = false;
try { await db.SaveChangesAsync(); }
catch (DbUpdateException) { rejected = true; }
Check(rejected, "unique active-assignment constraint rejects a second active row");
db.ChangeTracker.Clear();

// Compile equivalent queries using the production MySQL provider, without opening a connection.
var mysqlOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseMySql("Server=127.0.0.1;Database=unused;User=unused", new MySqlServerVersion(new Version(8, 0, 36))).Options;
await using var mysql = new ApplicationDbContext(mysqlOptions);
var sql = mysql.tblTenants.Include(t => t.Assignments).ThenInclude(a => a.Unit).ToQueryString();
Check(!sql.Contains("`t`.`UnitID`"), "MySQL tenant query does not select TenantProfiles.UnitID");
Check(mysql.tblUnits.Include(u => u.Assignments).ThenInclude(a => a.Tenant).ToQueryString().Contains("TenantUnitAssignments"),
    "MySQL unit occupant query compiles");
Check(mysql.tblBillings.Include(b => b.Tenant).ThenInclude(t => t!.Assignments).ThenInclude(a => a.Unit)
    .ToQueryString().Contains("TenantUnitAssignments"), "MySQL billing query compiles");
Check(mysql.tblUsers.Include(u => u.Tenants).ToQueryString().Contains("TenantUnitAssignments"), "MySQL account query compiles");
Console.WriteLine($"All {checks} checks passed. No localhost database was accessed.");
