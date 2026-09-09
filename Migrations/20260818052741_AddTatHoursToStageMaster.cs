using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddTatHoursToStageMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TatLargeHours",
                table: "SCIH_StageMaster",
                type: "numeric(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TatMediumHours",
                table: "SCIH_StageMaster",
                type: "numeric(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TatSmallHours",
                table: "SCIH_StageMaster",
                type: "numeric(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 16m, 8m, 4m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 16m, 8m, 4m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 16m, 8m, 4m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 10m, 5m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 10m, 5m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 9L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 10m, 5m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 10L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 6m, 3m, 1m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 11L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 12L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 12m, 6m, 3m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 13L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 4m, 2m, 1m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 14L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 15L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 6m, 3m, 1m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 16L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 17L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 18L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 6m, 3m, 1m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 19L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 12m, 6m, 3m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 20L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 4m, 2m, 1m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 21L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 22L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 23L,
                columns: new[] { "TatLargeHours", "TatMediumHours", "TatSmallHours" },
                values: new object[] { 8m, 4m, 2m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TatLargeHours",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "TatMediumHours",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "TatSmallHours",
                table: "SCIH_StageMaster");
        }
    }
}
