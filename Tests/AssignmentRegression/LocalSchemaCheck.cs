using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using YnclinoApartmentManagementSystem.Data;

internal static class LocalSchemaCheck
{
    public static async Task RunAsync()
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.Local.json", optional: true).Build();
        string connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No local database configured.");
        if (config["MySqlPassword"] is { Length: > 0 } password)
            connectionString = connectionString.Replace("YOUR_MYSQL_PASSWORD", password);
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (builder.Server is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Preflight is restricted to localhost.");
        if (builder.SslMode == MySqlSslMode.Disabled) builder.AllowPublicKeyRetrieval = true;
        builder.ConnectionTimeout = 5;
        builder.AllowUserVariables = true;
        builder.UseAffectedRows = false;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        // The server enforces read-only access for every query below.
        await using (var start = new MySqlCommand("START TRANSACTION READ ONLY", connection))
            await start.ExecuteNonQueryAsync();
        try
        {
            async Task<long> Count(string sql)
            {
                await using var command = new MySqlCommand(sql, connection);
                return Convert.ToInt64(await command.ExecuteScalarAsync());
            }
            bool hasColumn = await Count("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='TenantProfiles' AND COLUMN_NAME='UnitID'") > 0;
            Console.WriteLine(hasColumn ? "Legacy profile UnitID column still present." : "Profile UnitID column already absent.");
            long mismatches = 0;
            if (hasColumn)
            {
                mismatches = await Count("""
                    SELECT COUNT(*) FROM TenantProfiles t
                    LEFT JOIN TenantUnitAssignments a ON a.TenantID=t.TenantID AND a.Status='Active'
                    WHERE (t.Status='Active' AND NOT (t.UnitID <=> a.UnitID))
                       OR (t.Status<>'Active' AND a.AssignmentID IS NOT NULL)
                       OR (t.Status<>'Active' AND t.UnitID IS NOT NULL AND NOT (
                           t.UnitID <=> (SELECT h.UnitID FROM TenantUnitAssignments h
                               WHERE h.TenantID=t.TenantID ORDER BY h.AssignmentID DESC LIMIT 1)))
                    """);
                Console.WriteLine($"Profile/assignment mismatches: {mismatches}");
            }
            long uniqueGuard = await Count("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='TenantUnitAssignments' AND INDEX_NAME='uq_tenant_one_active_assignment' AND COLUMN_NAME='ActiveTenantID' AND NON_UNIQUE=0");
            Console.WriteLine($"Unique active-assignment safeguard present: {uniqueGuard > 0}");

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 36))).Options;
            await using var db = new ApplicationDbContext(options);
            await db.tblTenants.AsNoTracking().Take(3).ToListAsync();
            // Unit -> assignments -> tenant -> assignments/unit is a shared graph.
            await db.tblUnits.AsNoTrackingWithIdentityResolution().Include(u => u.Assignments).ThenInclude(a => a.Tenant).Take(3).ToListAsync();
            await db.tblBillings.AsNoTracking().Include(b => b.Tenant).Take(3).ToListAsync();
            await db.tblUsers.AsNoTracking().Include(u => u.Tenants).Take(3).ToListAsync();
            await db.tblMaintenanceRequests.AsNoTracking().Include(m => m.Unit).Include(m => m.Tenant).Take(3).ToListAsync();
            await db.tblUnitTransferRequests.AsNoTracking().Include(r => r.Tenant).Include(r => r.CurrentUnit).Include(r => r.RequestedUnit).Take(3).ToListAsync();
            await db.tblTenants.Where(t => t.Assignments.Any(a => a.Status == "Active"))
                .Select(t => t.Assignments.Where(a => a.Status == "Active").Select(a => a.Unit!.UnitNumber).FirstOrDefault())
                .Take(3).ToListAsync();
            Console.WriteLine("Production MySQL read queries passed (tenant, unit, billing, account, maintenance, transfer, dropdown).");
            Console.WriteLine("No schema or data changes made.");
            if (mismatches > 0 || uniqueGuard == 0) Environment.ExitCode = 1;
        }
        finally
        {
            await using var rollback = new MySqlCommand("ROLLBACK", connection);
            await rollback.ExecuteNonQueryAsync();
        }
    }
}
