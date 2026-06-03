using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YnclinoAMS.Migrations
{
    public partial class AddClaimRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tblClaimRequests",
                columns: table => new
                {
                    ClaimID            = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ItemID             = table.Column<int>(nullable: false),
                    ClaimantUserID     = table.Column<int>(nullable: false),
                    VerificationDetails = table.Column<string>(maxLength: 1000, nullable: false),
                    SubmittedAt        = table.Column<DateTime>(nullable: false, defaultValueSql: "datetime('now','localtime')"),
                    Status             = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Pending"),
                    AdminNotes         = table.Column<string>(maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tblClaimRequests", x => x.ClaimID);
                    table.ForeignKey("FK_tblClaimRequests_tblLostFoundItems_ItemID", x => x.ItemID, "tblLostFoundItems", "ItemID", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_tblClaimRequests_tblUsers_ClaimantUserID", x => x.ClaimantUserID, "tblUsers", "UserID", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_tblClaimRequests_ItemID", "tblClaimRequests", "ItemID");
            migrationBuilder.CreateIndex("IX_tblClaimRequests_ClaimantUserID", "tblClaimRequests", "ClaimantUserID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("tblClaimRequests");
        }
    }
}
