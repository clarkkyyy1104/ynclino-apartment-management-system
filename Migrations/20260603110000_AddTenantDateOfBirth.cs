using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoAMS.Migrations
{
    public partial class AddTenantDateOfBirth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "tblTenants",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "tblTenants");
        }
    }
}
