using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerStaffProfileToIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerStaffProfileId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OwnerStaffProfileId",
                table: "Issues",
                column: "OwnerStaffProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_StaffProfiles_OwnerStaffProfileId",
                table: "Issues",
                column: "OwnerStaffProfileId",
                principalTable: "StaffProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_StaffProfiles_OwnerStaffProfileId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_OwnerStaffProfileId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "OwnerStaffProfileId",
                table: "Issues");
        }
    }
}
