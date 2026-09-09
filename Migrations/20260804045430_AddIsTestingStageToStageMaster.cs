using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddIsTestingStageToStageMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTestingStage",
                table: "SCIH_StageMaster",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 1L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 2L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 3L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 4L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 5L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 6L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 7L,
                column: "IsTestingStage",
                value: true);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 8L,
                column: "IsTestingStage",
                value: true);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 9L,
                column: "IsTestingStage",
                value: true);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 10L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 11L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 12L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 13L,
                column: "IsTestingStage",
                value: true);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 14L,
                column: "IsTestingStage",
                value: false);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 15L,
                column: "IsTestingStage",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTestingStage",
                table: "SCIH_StageMaster");
        }
    }
}
