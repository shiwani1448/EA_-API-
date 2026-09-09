using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyCandidateStatusFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TelephonicAssessments");
            migrationBuilder.DropTable(name: "TelephonicCallAttempts");
            migrationBuilder.DropTable(name: "TelephonicRounds");
            migrationBuilder.DropTable(name: "ScreeningDecisions");
            migrationBuilder.DropTable(name: "PipelineStageLogs");
            migrationBuilder.DropTable(name: "CandidatePipelines");

            migrationBuilder.AddColumn<DateTime>(
                name: "ShortlistingDate",
                table: "Candidates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortlistingDate",
                table: "Candidates");

            migrationBuilder.CreateTable(
                name: "CandidatePipelines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignedTo = table.Column<int>(type: "integer", nullable: true),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "applied"),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidatePipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineStageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActionedBy = table.Column<int>(type: "integer", nullable: true),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    FromStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    ToStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineStageLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    ScreenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScreenedBy = table.Column<int>(type: "integer", nullable: true),
                    TimeToScreenMins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelephonicAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssessedBy = table.Column<int>(type: "integer", nullable: false),
                    CallAttemptId = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    CommunicationRating = table.Column<int>(type: "integer", nullable: false),
                    CtcExpectationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CtcRevisedLpa = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    DetailedNotes = table.Column<string>(type: "text", nullable: true),
                    InterviewAvailability = table.Column<string>(type: "text", nullable: true),
                    NoticePeriodActual = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NoticePeriodConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    OverallImpression = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RoleClarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TelephonicRoundId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelephonicCallAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CallStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalledBy = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    NextFollowupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TelephonicRoundId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicCallAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelephonicRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignedHr = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUnreachable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicRounds", x => x.Id);
                });
        }
    }
}
