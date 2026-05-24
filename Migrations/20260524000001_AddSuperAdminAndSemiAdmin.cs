using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoAMS.Migrations
{
    public partial class AddSuperAdminAndSemiAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "tblUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Mark the existing seeded admin account as super admin
            migrationBuilder.Sql(
                "UPDATE tblUsers SET IsSuperAdmin = 1 WHERE Username = 'admin' AND Role = 'Admin'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "tblUsers");
        }
    }
}
