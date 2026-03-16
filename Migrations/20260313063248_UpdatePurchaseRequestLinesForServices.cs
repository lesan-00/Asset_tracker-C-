using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePurchaseRequestLinesForServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ItemName",
                table: "PurchaseRequestLines",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "LineType",
                table: "PurchaseRequestLines",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Item")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineType",
                table: "PurchaseRequestLines");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "PurchaseRequestLines",
                newName: "ItemName");
        }
    }
}
