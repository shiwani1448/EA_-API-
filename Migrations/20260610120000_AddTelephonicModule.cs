using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddTelephonicModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Create TelephonicRounds ───────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TelephonicRounds",
                columns: table => new
                {
                    Id             = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId    = table.Column<int>(type: "integer", nullable: false),
                    RequisitionId  = table.Column<int>(type: "integer", nullable: false),
                    AssignedHr     = table.Column<int>(type: "integer", nullable: false),
                    Status         = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    TotalAttempts  = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastAttemptAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsUnreachable  = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Outcome        = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicRounds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicRounds_CandidateId",
                table: "TelephonicRounds",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicRounds_RequisitionId",
                table: "TelephonicRounds",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicRounds_AssignedHr",
                table: "TelephonicRounds",
                column: "AssignedHr");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicRounds_RequisitionId_Status",
                table: "TelephonicRounds",
                columns: new[] { "RequisitionId", "Status" });

            // ── Create TelephonicCallAttempts ─────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TelephonicCallAttempts",
                columns: table => new
                {
                    Id                = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelephonicRoundId = table.Column<int>(type: "integer", nullable: false),
                    CandidateId       = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber     = table.Column<int>(type: "integer", nullable: false),
                    CalledAt          = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalledBy          = table.Column<int>(type: "integer", nullable: false),
                    CallStatus        = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DurationMinutes   = table.Column<int>(type: "integer", nullable: true),
                    Notes             = table.Column<string>(type: "text", nullable: true),
                    NextFollowupAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicCallAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicCallAttempts_TelephonicRoundId",
                table: "TelephonicCallAttempts",
                column: "TelephonicRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicCallAttempts_CandidateId",
                table: "TelephonicCallAttempts",
                column: "CandidateId");

            // ── Create TelephonicAssessments ──────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TelephonicAssessments",
                columns: table => new
                {
                    Id                      = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelephonicRoundId       = table.Column<int>(type: "integer", nullable: false),
                    CandidateId             = table.Column<int>(type: "integer", nullable: false),
                    CallAttemptId           = table.Column<int>(type: "integer", nullable: false),
                    CommunicationRating     = table.Column<int>(type: "integer", nullable: false),
                    RoleClarity             = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CtcExpectationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CtcRevisedLpa           = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    NoticePeriodConfirmed   = table.Column<bool>(type: "boolean", nullable: false),
                    NoticePeriodActual      = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InterviewAvailability   = table.Column<string>(type: "text", nullable: true),
                    OverallImpression       = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recommendation          = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DetailedNotes           = table.Column<string>(type: "text", nullable: true),
                    AssessedBy              = table.Column<int>(type: "integer", nullable: false),
                    AssessedAt              = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonicAssessments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicAssessments_TelephonicRoundId",
                table: "TelephonicAssessments",
                column: "TelephonicRoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicAssessments_CandidateId",
                table: "TelephonicAssessments",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonicAssessments_CallAttemptId",
                table: "TelephonicAssessments",
                column: "CallAttemptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TelephonicAssessments");
            migrationBuilder.DropTable(name: "TelephonicCallAttempts");
            migrationBuilder.DropTable(name: "TelephonicRounds");
        }
    }
}
