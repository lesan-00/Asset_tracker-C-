using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueCategoryAndReportedFor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Issues",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "General")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReportedForStaffProfileId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `Issues`
                SET `ReportedForStaffProfileId` = `OwnerStaffProfileId`
                WHERE `OwnerStaffProfileId` IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ReportedForStaffProfileId",
                table: "Issues",
                column: "ReportedForStaffProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_StaffProfiles_ReportedForStaffProfileId",
                table: "Issues",
                column: "ReportedForStaffProfileId",
                principalTable: "StaffProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerStaffProfileId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `Issues`
                SET `OwnerStaffProfileId` = `ReportedForStaffProfileId`
                WHERE `ReportedForStaffProfileId` IS NOT NULL;
                """);

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

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_StaffProfiles_ReportedForStaffProfileId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ReportedForStaffProfileId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ReportedForStaffProfileId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Issues");
        }
    }
}
