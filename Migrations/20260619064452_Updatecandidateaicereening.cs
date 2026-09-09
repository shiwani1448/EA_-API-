using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class Updatecandidateaicereening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AIStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiPromptLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiResponseLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BatchId",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidateName",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "CandidateAIScreenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStep",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionMessage",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionType",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionMethodUsed",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureStep",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InnerExceptionMessage",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JDExtractionMethod",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JDExtractionStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JDTextAvailable",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NormalTextLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OCRTextLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PdfPageCount",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RenderedImageCreated",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RenderedImagePath",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtractionMethod",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtractionStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResumeTextAvailable",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "CandidateAIScreenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DirectorAiInterviewQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    InterviewRoundId = table.Column<int>(type: "integer", nullable: false),
                    ScreeningScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DiscProfile = table.Column<string>(type: "text", nullable: true),
                    DScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    HrRoundScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    TechnicalAssessmentScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    OverallGapSummary = table.Column<string>(type: "text", nullable: true),
                    QuestionNo = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    WhyDirectorShouldAskThis = table.Column<string>(type: "text", nullable: true),
                    GapOrRiskArea = table.Column<string>(type: "text", nullable: true),
                    StrongAnswerSignals = table.Column<string>(type: "text", nullable: true),
                    RedFlagSignals = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorAiInterviewQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningBatches",
                columns: table => new
                {
                    BatchId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalCandidates = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    PendingReview = table.Column<int>(type: "integer", nullable: false),
                    Shortlisted = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    ForceRescreen = table.Column<bool>(type: "boolean", nullable: false),
                    CandidateIdsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningBatches", x => x.BatchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAIScreenings_BatchId",
                table: "CandidateAIScreenings",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestionBanks_Department_Designation_Level_Round~1",
                table: "AssessmentQuestionBanks",
                columns: new[] { "Department", "Designation", "Level", "RoundName", "QuestionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_CandidateId",
                table: "DirectorAiInterviewQuestions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_CandidateId_InterviewRoundId_Q~",
                table: "DirectorAiInterviewQuestions",
                columns: new[] { "CandidateId", "InterviewRoundId", "QuestionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_InterviewRoundId",
                table: "DirectorAiInterviewQuestions",
                column: "InterviewRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningBatches_CreatedAt",
                table: "ScreeningBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningBatches_Status",
                table: "ScreeningBatches",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectorAiInterviewQuestions");

            migrationBuilder.DropTable(
                name: "ScreeningBatches");

            migrationBuilder.DropIndex(
                name: "IX_CandidateAIScreenings_BatchId",
                table: "CandidateAIScreenings");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentQuestionBanks_Department_Designation_Level_Round~1",
                table: "AssessmentQuestionBanks");

            migrationBuilder.DropColumn(
                name: "AIStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "AiPromptLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "AiResponseLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CandidateName",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CurrentStep",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExceptionMessage",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExceptionType",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExtractionMethodUsed",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "FailureStep",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "InnerExceptionMessage",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDExtractionMethod",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDExtractionStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDTextAvailable",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "NormalTextLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "OCRTextLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "PdfPageCount",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "RenderedImageCreated",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "RenderedImagePath",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeExtractionMethod",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeExtractionStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeTextAvailable",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ScreeningStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "CandidateAIScreenings");
        }
    }
}
