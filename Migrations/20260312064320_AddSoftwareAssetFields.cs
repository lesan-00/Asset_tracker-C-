using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftwareAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserOrDepartment",
                table: "Assets",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "Assets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Assets",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BuildNumber",
                table: "Assets",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Assets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Assets",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "KES")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeploymentEnvironment",
                table: "Assets",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "Assets",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstalledOn",
                table: "Assets",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceReference",
                table: "Assets",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedDate",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseKey",
                table: "Assets",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LicenseType",
                table: "Assets",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MaintainerOrVendor",
                table: "Assets",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OpenSourceLicenseType",
                table: "Assets",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PatchStatus",
                table: "Assets",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlanOrTier",
                table: "Assets",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderReference",
                table: "Assets",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalReminderDate",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStatus",
                table: "Assets",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareCategory",
                table: "Assets",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareName",
                table: "Assets",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareNotes",
                table: "Assets",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareStatus",
                table: "Assets",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SoftwareVersion",
                table: "Assets",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupportEndDate",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Assets",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedToUserOrDepartment",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BuildNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DeploymentEnvironment",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Edition",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InstalledOn",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InvoiceReference",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LastUpdatedDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LicenseKey",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LicenseType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "MaintainerOrVendor",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OpenSourceLicenseType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PatchStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PlanOrTier",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderReference",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RenewalReminderDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SecurityStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SoftwareCategory",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SoftwareName",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SoftwareNotes",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SoftwareStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SoftwareVersion",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SupportEndDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Assets");
        }
    }
}
