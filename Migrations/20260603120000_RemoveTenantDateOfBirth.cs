using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoAMS.Migrations
{
    public partial class RemoveTenantDateOfBirth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "tblTenants");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "tblTenants",
                type: "TEXT",
                nullable: true);
        }
    }
}
