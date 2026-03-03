using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTracker.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAssignmentStatusesToActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `Assignments`
                SET `Status` = 5
                WHERE `Status` IN (0, 1, 2);
                """);

            migrationBuilder.Sql("""
                UPDATE `Assignments`
                SET `Status` = 7
                WHERE `Status` = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `Assignments`
                SET `Status` = 0
                WHERE `Status` = 5;
                """);

            migrationBuilder.Sql("""
                UPDATE `Assignments`
                SET `Status` = 3
                WHERE `Status` = 7;
                """);
        }
    }
}
