using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddSnagListModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SCIH_SnagList",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<string>(type: "varchar(100)", nullable: false),
                    TaskId = table.Column<string>(type: "varchar(100)", nullable: false),
                    Module = table.Column<string>(type: "varchar(300)", nullable: false),
                    SnagDescription = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "varchar(20)", nullable: false),
                    CurrentStatus = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Open"),
                    StageDetails = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    CreationDate = table.Column<string>(type: "varchar(50)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", nullable: false),
                    UpdationDate = table.Column<string>(type: "varchar(50)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", nullable: true),
                    IsDelete = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "false"),
                    IsDeletedBy = table.Column<string>(type: "varchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_SnagList", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SCIH_StageMaster",
                columns: new[] { "Id", "ChecklistJson", "CreatedBy", "CreatedDate", "DisplayOrder", "IsActive", "IsDeleted", "IsTestingStage", "ModifiedBy", "ModifiedDate", "StageName" },
                values: new object[,]
                {
                    { 16L, "[\"Issue Analysed\",\"Code Fixed\",\"Unit Tested\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, false, false, null, null, "Snag Fix" },
                    { 17L, "[\"Root Cause Identified\",\"Resolution Documented\",\"Reviewed by Lead\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 17, true, false, false, null, null, "RRR" },
                    { 18L, "[\"Code Merged\",\"Deployment Completed\",\"Smoke Testing Completed\",\"Rollback Plan Ready\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 18, true, false, false, null, null, "Merge & Make Live" },
                    { 19L, "[\"Test Cases Executed\",\"Defect Fix Verified\",\"Regression Passed\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 19, true, false, true, null, null, "Testing" },
                    { 20L, "[\"Business User Reviewed\",\"Feedback Documented\",\"Sign-off Received\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 20, true, false, false, null, null, "Final Feedback" },
                    { 21L, "[\"Technical Doc Updated\",\"Developer Video Recorded\",\"Reviewed by Lead\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 21, true, false, false, null, null, "Technical Documentation" },
                    { 22L, "[\"Documentation Completeness Checked\",\"AI Quality Review Passed\",\"Flagged Issues Resolved\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 22, true, false, false, null, null, "AI Documentation Review" },
                    { 23L, "[\"User Guide Prepared\",\"Training Video Recorded\",\"Reviewed by Lead\"]", "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Utc), 23, true, false, false, null, null, "User Training Documentation" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_SnagList_CurrentStatus",
                table: "SCIH_SnagList",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_SnagList_RequestId",
                table: "SCIH_SnagList",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_SnagList_TaskId",
                table: "SCIH_SnagList",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SCIH_SnagList");

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 23L);
        }
    }
}
