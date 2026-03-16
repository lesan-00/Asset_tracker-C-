using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetTaxonomyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Assets",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                table: "Assets",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TopLevelCategory",
                table: "Assets",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE `Assets`
                SET
                    `TopLevelCategory` = 'hardware',
                    `Category` = CASE
                        WHEN `AssetType` = 'Laptop' THEN 'laptops'
                        WHEN `AssetType` = 'Desktop' THEN 'desktops'
                        WHEN `AssetType` = 'SystemUnit' THEN 'desktops'
                        WHEN `AssetType` = 'Monitor' THEN 'monitors'
                        WHEN `AssetType` = 'Printer' THEN 'printers'
                        WHEN `AssetType` = 'Router' THEN 'servers'
                        WHEN `AssetType` = 'Switch' THEN 'servers'
                        ELSE 'peripherals'
                    END,
                    `SubCategory` = NULL
                WHERE `TopLevelCategory` IS NULL OR `TopLevelCategory` = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TopLevelCategory",
                table: "Assets");
        }
    }
}
