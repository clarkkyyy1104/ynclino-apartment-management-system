using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoAMS.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblUnits",
                columns: table => new
                {
                    UnitID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RentPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Vacant"),
                    DateAdded = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblUnits", x => x.UnitID);
                });

            migrationBuilder.CreateTable(
                name: "tblUsers",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblUsers", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "tblTenants",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: true),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EmergencyContact = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MoveInDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MoveOutDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    DateRecorded = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblTenants", x => x.TenantID);
                    table.ForeignKey(
                        name: "FK_tblTenants_tblUnits_UnitID",
                        column: x => x.UnitID,
                        principalTable: "tblUnits",
                        principalColumn: "UnitID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tblTenants_tblUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "tblUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tblTenants_UnitID",
                table: "tblTenants",
                column: "UnitID");

            migrationBuilder.CreateIndex(
                name: "IX_tblTenants_UserID",
                table: "tblTenants",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_tblUnits_UnitNumber",
                table: "tblUnits",
                column: "UnitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tblUsers_Username",
                table: "tblUsers",
                column: "Username",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tblTenants");
            migrationBuilder.DropTable(name: "tblUnits");
            migrationBuilder.DropTable(name: "tblUsers");
        }
    }
}
