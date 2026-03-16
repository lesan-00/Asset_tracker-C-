using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorContractFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "VendorContracts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ContractType",
                table: "VendorContracts",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ContractValue",
                table: "VendorContracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "VendorContracts",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "KES")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "VendorContracts",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedDate",
                table: "VendorContracts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorContracts_ContractNumber",
                table: "VendorContracts",
                column: "ContractNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorContracts_ContractNumber",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "VendorContracts");

            migrationBuilder.DropColumn(
                name: "SignedDate",
                table: "VendorContracts");
        }
    }
}
