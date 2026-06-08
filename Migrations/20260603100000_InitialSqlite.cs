using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoApartmentManagementSystem.Migrations
{
    public partial class InitialSqlite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblUsers",
                columns: table => new
                {
                    UserID       = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Username     = table.Column<string>(maxLength: 50, nullable: false),
                    Password     = table.Column<string>(maxLength: 255, nullable: false),
                    Role         = table.Column<string>(maxLength: 20, nullable: false),
                    IsActive     = table.Column<bool>(nullable: false, defaultValue: true),
                    IsSuperAdmin = table.Column<bool>(nullable: false, defaultValue: false),
                    DateCreated  = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')")
                },
                constraints: table => table.PrimaryKey("PK_tblUsers", x => x.UserID));

            migrationBuilder.CreateIndex("IX_tblUsers_Username", "tblUsers", "Username", unique: true);

            migrationBuilder.CreateTable(
                name: "tblUnits",
                columns: table => new
                {
                    UnitID    = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    UnitNumber = table.Column<string>(maxLength: 20, nullable: false),
                    UnitType  = table.Column<string>(maxLength: 50, nullable: false),
                    RentPrice = table.Column<decimal>(nullable: false),
                    Capacity  = table.Column<int>(nullable: false),
                    Status    = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Vacant"),
                    DateAdded = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')")
                },
                constraints: table => table.PrimaryKey("PK_tblUnits", x => x.UnitID));

            migrationBuilder.CreateIndex("IX_tblUnits_UnitNumber", "tblUnits", "UnitNumber", unique: true);

            migrationBuilder.CreateTable(
                name: "tblTenants",
                columns: table => new
                {
                    TenantID         = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    UserID           = table.Column<int>(nullable: true),
                    UnitID           = table.Column<int>(nullable: false),
                    FirstName        = table.Column<string>(maxLength: 50, nullable: false),
                    LastName         = table.Column<string>(maxLength: 50, nullable: false),
                    ContactNumber    = table.Column<string>(maxLength: 20, nullable: true),
                    EmergencyContact = table.Column<string>(maxLength: 100, nullable: true),
                    MoveInDate       = table.Column<DateTime>(nullable: true),
                    MoveOutDate      = table.Column<DateTime>(nullable: true),
                    LeaseStart       = table.Column<DateTime>(nullable: true),
                    LeaseEnd         = table.Column<DateTime>(nullable: true),
                    Status           = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Active"),
                    DateRecorded     = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblTenants", x => x.TenantID);
                    table.ForeignKey("FK_tblTenants_tblUsers_UserID",   x => x.UserID,  "tblUsers", "UserID", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_tblTenants_tblUnits_UnitID",   x => x.UnitID,  "tblUnits", "UnitID", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_tblTenants_UserID", "tblTenants", "UserID");
            migrationBuilder.CreateIndex("IX_tblTenants_UnitID", "tblTenants", "UnitID");

            migrationBuilder.CreateTable(
                name: "tblBillings",
                columns: table => new
                {
                    BillingID     = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TenantID      = table.Column<int>(nullable: false),
                    BillingPeriod = table.Column<DateTime>(nullable: false),
                    AmountDue     = table.Column<decimal>(nullable: false),
                    DueDate       = table.Column<DateTime>(nullable: false),
                    AmountPaid    = table.Column<decimal>(nullable: true),
                    DatePaid      = table.Column<DateTime>(nullable: true),
                    Status        = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Unpaid"),
                    Notes         = table.Column<string>(maxLength: 500, nullable: true),
                    DateIssued    = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblBillings", x => x.BillingID);
                    table.ForeignKey("FK_tblBillings_tblTenants_TenantID", x => x.TenantID, "tblTenants", "TenantID", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_tblBillings_TenantID", "tblBillings", "TenantID");

            migrationBuilder.CreateTable(
                name: "tblMaintenanceRequests",
                columns: table => new
                {
                    RequestID     = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TenantID      = table.Column<int>(nullable: false),
                    Category      = table.Column<string>(maxLength: 50, nullable: false),
                    Description   = table.Column<string>(maxLength: 500, nullable: false),
                    Priority      = table.Column<string>(maxLength: 20, nullable: false),
                    Status        = table.Column<string>(maxLength: 30, nullable: false, defaultValue: "Pending"),
                    DateSubmitted = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')"),
                    DateResolved  = table.Column<DateTime>(nullable: true),
                    AdminNotes    = table.Column<string>(maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblMaintenanceRequests", x => x.RequestID);
                    table.ForeignKey("FK_tblMaintenanceRequests_tblTenants_TenantID", x => x.TenantID, "tblTenants", "TenantID", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_tblMaintenanceRequests_TenantID", "tblMaintenanceRequests", "TenantID");

            migrationBuilder.CreateTable(
                name: "tblLostFoundItems",
                columns: table => new
                {
                    ItemID           = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ReportedByUserID = table.Column<int>(nullable: false),
                    ItemName         = table.Column<string>(maxLength: 100, nullable: false),
                    Description      = table.Column<string>(maxLength: 500, nullable: true),
                    ItemType         = table.Column<string>(maxLength: 10, nullable: false),
                    Location         = table.Column<string>(maxLength: 200, nullable: true),
                    Status           = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Reported"),
                    DateReported     = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')"),
                    Notes            = table.Column<string>(maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblLostFoundItems", x => x.ItemID);
                    table.ForeignKey("FK_tblLostFoundItems_tblUsers_ReportedByUserID", x => x.ReportedByUserID, "tblUsers", "UserID", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_tblLostFoundItems_ReportedByUserID", "tblLostFoundItems", "ReportedByUserID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("tblLostFoundItems");
            migrationBuilder.DropTable("tblMaintenanceRequests");
            migrationBuilder.DropTable("tblBillings");
            migrationBuilder.DropTable("tblTenants");
            migrationBuilder.DropTable("tblUnits");
            migrationBuilder.DropTable("tblUsers");
        }
    }
}
