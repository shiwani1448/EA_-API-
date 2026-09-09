using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Alter Candidates ─────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "CurrentStage",
                table: "Candidates",
                type: "text",
                nullable: false,
                defaultValue: "applied");

            migrationBuilder.AddColumn<string>(
                name: "CurrentStatus",
                table: "Candidates",
                type: "text",
                nullable: false,
                defaultValue: "pending");

            // ── Alter HiringRequests ─────────────────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "ExperienceMinYears",
                table: "HiringRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceMaxYears",
                table: "HiringRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetMinLpa",
                table: "HiringRequests",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetMaxLpa",
                table: "HiringRequests",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptableNoticePeriods",
                table: "HiringRequests",
                type: "text",
                nullable: true);

            // ── Create CandidatePipelines ─────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CandidatePipelines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId   = table.Column<int>(type: "integer", nullable: false),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    CurrentStage  = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "applied"),
                    Status        = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    AssignedTo    = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidatePipelines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidatePipelines_CandidateId",
                table: "CandidatePipelines",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidatePipelines_RequisitionId",
                table: "CandidatePipelines",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidatePipelines_RequisitionId_CurrentStage_Status",
                table: "CandidatePipelines",
                columns: new[] { "RequisitionId", "CurrentStage", "Status" });

            // ── Create PipelineStageLogs ─────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PipelineStageLogs",
                columns: table => new
                {
                    Id            = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId   = table.Column<int>(type: "integer", nullable: false),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    FromStage     = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStage       = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action        = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason        = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Notes         = table.Column<string>(type: "text", nullable: true),
                    ActionedBy    = table.Column<int>(type: "integer", nullable: true),
                    ActionedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineStageLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineStageLogs_CandidateId",
                table: "PipelineStageLogs",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineStageLogs_RequisitionId",
                table: "PipelineStageLogs",
                column: "RequisitionId");

            // ── Create ScreeningDecisions ─────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ScreeningDecisions",
                columns: table => new
                {
                    Id               = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId      = table.Column<int>(type: "integer", nullable: false),
                    RequisitionId    = table.Column<int>(type: "integer", nullable: false),
                    Decision         = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectionReason  = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes            = table.Column<string>(type: "text", nullable: true),
                    ScreenedBy       = table.Column<int>(type: "integer", nullable: true),
                    ScreenedAt       = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeToScreenMins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningDecisions_CandidateId",
                table: "ScreeningDecisions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningDecisions_RequisitionId",
                table: "ScreeningDecisions",
                column: "RequisitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScreeningDecisions");
            migrationBuilder.DropTable(name: "PipelineStageLogs");
            migrationBuilder.DropTable(name: "CandidatePipelines");

            migrationBuilder.DropColumn(name: "CurrentStage",  table: "Candidates");
            migrationBuilder.DropColumn(name: "CurrentStatus", table: "Candidates");

            migrationBuilder.DropColumn(name: "ExperienceMinYears",      table: "HiringRequests");
            migrationBuilder.DropColumn(name: "ExperienceMaxYears",      table: "HiringRequests");
            migrationBuilder.DropColumn(name: "BudgetMinLpa",            table: "HiringRequests");
            migrationBuilder.DropColumn(name: "BudgetMaxLpa",            table: "HiringRequests");
            migrationBuilder.DropColumn(name: "AcceptableNoticePeriods", table: "HiringRequests");
        }
    }
}
