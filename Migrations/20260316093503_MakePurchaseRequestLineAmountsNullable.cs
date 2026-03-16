using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class MakePurchaseRequestLineAmountsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedTotal",
                table: "PurchaseRequestLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedUnitCost",
                table: "PurchaseRequestLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "PurchaseRequestLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `PurchaseRequestLines`
                SET `EstimatedTotal` = 0
                WHERE `EstimatedTotal` IS NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE `PurchaseRequestLines`
                SET `EstimatedUnitCost` = 0
                WHERE `EstimatedUnitCost` IS NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE `PurchaseRequestLines`
                SET `Quantity` = 1
                WHERE `Quantity` IS NULL OR `Quantity` <= 0;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedTotal",
                table: "PurchaseRequestLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedUnitCost",
                table: "PurchaseRequestLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "PurchaseRequestLines",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
