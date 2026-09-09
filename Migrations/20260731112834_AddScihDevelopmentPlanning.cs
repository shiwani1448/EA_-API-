using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddScihDevelopmentPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SCIH_StageMaster",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StageName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ChecklistJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_StageMaster", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SCIH_Task",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleName = table.Column<string>(type: "varchar(300)", nullable: false),
                    ProjectLeadId = table.Column<long>(type: "bigint", nullable: false),
                    OverallStartDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    OverallEndDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Priority = table.Column<string>(type: "varchar(20)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Pending"),
                    StageDetailsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_Task", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SCIH_Task_SCIH_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "SCIH_Request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCIH_TaskHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    StageId = table.Column<long>(type: "bigint", nullable: true),
                    StageName = table.Column<string>(type: "varchar(100)", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PreviousValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewValue = table.Column<string>(type: "jsonb", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    ActionBy = table.Column<long>(type: "bigint", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    IPAddress = table.Column<string>(type: "varchar(100)", nullable: true),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_TaskHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SCIH_TaskHistory_SCIH_Task_TaskId",
                        column: x => x.TaskId,
                        principalTable: "SCIH_Task",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SCIH_StageMaster",
                columns: new[] { "Id", "ChecklistJson", "DisplayOrder", "IsActive", "StageName" },
                values: new object[,]
                {
                    { 1L, "[\"Requirement Reviewed\",\"Flow Prepared\",\"Approved by Lead\"]", 1, true, "Mind Mapping" },
                    { 2L, "[\"API Created\",\"Validation Completed\",\"Repository Added\",\"Service Added\",\"Unit Testing Completed\"]", 2, true, "Backend" },
                    { 3L, "[\"Screen Completed\",\"Responsive Completed\",\"API Integrated\",\"Validation Added\"]", 3, true, "Frontend" },
                    { 4L, "[\"Model Selected\",\"Prompt Designed\",\"Model Integrated\",\"Output Validated\"]", 4, true, "AI" },
                    { 5L, "[\"Schema Designed\",\"Tables Created\",\"Indexes Added\",\"Migration Verified\"]", 5, true, "Database" },
                    { 6L, "[\"Endpoints Mapped\",\"Integration Completed\",\"Error Handling Verified\",\"Response Validated\"]", 6, true, "API Integration" },
                    { 7L, "[\"Test Cases Prepared\",\"Unit Tests Passed\",\"Bug Fixes Verified\"]", 7, true, "Backend Testing" },
                    { 8L, "[\"Test Cases Prepared\",\"UI Tests Passed\",\"Cross Browser Verified\"]", 8, true, "Frontend Testing" },
                    { 9L, "[\"Test Dataset Prepared\",\"Accuracy Verified\",\"Edge Cases Tested\"]", 9, true, "AI Testing" },
                    { 10L, "[\"Build Verified\",\"Deployment Script Ready\",\"Deployed to Server\",\"Smoke Test Passed\"]", 10, true, "Deployment" },
                    { 11L, "[\"User Guide Prepared\",\"Technical Doc Updated\",\"Reviewed by Lead\"]", 11, true, "Documentation" },
                    { 12L, "[\"Script Prepared\",\"Recording Completed\",\"Editing Completed\"]", 12, true, "Video" },
                    { 13L, "[\"Video Reviewed\",\"Audio Verified\",\"Approved by Lead\"]", 13, true, "Video Testing" },
                    { 14L, "[\"Training Material Prepared\",\"Session Conducted\",\"Feedback Collected\"]", 14, true, "Training" },
                    { 15L, "[\"Documents Handed Over\",\"Access Provided\",\"Client Sign-off Received\"]", 15, true, "Handover" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_StageMaster_StageName",
                table: "SCIH_StageMaster",
                column: "StageName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Task_RequestId",
                table: "SCIH_Task",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Task_Status",
                table: "SCIH_Task",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_TaskHistory_ActionDate",
                table: "SCIH_TaskHistory",
                column: "ActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_TaskHistory_RequestId",
                table: "SCIH_TaskHistory",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_TaskHistory_TaskId",
                table: "SCIH_TaskHistory",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SCIH_StageMaster");

            migrationBuilder.DropTable(
                name: "SCIH_TaskHistory");

            migrationBuilder.DropTable(
                name: "SCIH_Task");
        }
    }
}
